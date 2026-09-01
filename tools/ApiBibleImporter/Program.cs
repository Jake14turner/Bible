using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

if (args.Length == 2 && string.Equals(args[0], "--repair-existing", StringComparison.OrdinalIgnoreCase))
{
    RepairExistingImport(Path.GetFullPath(args[1]));
    return;
}

var options = ImportOptions.Parse(args);
var outputPath = options.OutputPath ?? Path.Combine(
    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
    "Bible Study Studio",
    "LicensedData",
    "NASB1995",
    "nasb1995.json");
outputPath = Path.GetFullPath(outputPath);

if (!options.Force && !options.ListOnly && TryReadFreshImport(outputPath, out var freshUntil))
{
    Console.WriteLine($"NASB 1995 is current through {freshUntil:yyyy-MM-dd}. No API calls were made.");
    Console.WriteLine(outputPath);
    return;
}

var apiKey = Environment.GetEnvironmentVariable("BIBLE_API_KEY");
if (string.IsNullOrWhiteSpace(apiKey))
{
    apiKey = ReadSecret("API.Bible key: ");
}

if (string.IsNullOrWhiteSpace(apiKey))
{
    throw new InvalidOperationException("An API.Bible key is required. The key was not saved.");
}

using var client = new HttpClient
{
    BaseAddress = new Uri(options.Endpoint.TrimEnd('/') + "/"),
    Timeout = TimeSpan.FromSeconds(90)
};
client.DefaultRequestHeaders.Add("api-key", apiKey.Trim());

var biblesResponse = await GetJsonAsync(client, "v1/bibles?language=eng&include-full-details=true");
var bibles = ParseBibles(biblesResponse);
if (options.ListOnly)
{
    PrintBibles(bibles);
    return;
}

var selectedBible = SelectNasb1995Bible(bibles, options.BibleId);
Console.WriteLine($"Selected {selectedBible.Abbreviation}: {selectedBible.Name} ({selectedBible.Id})");
Console.WriteLine($"Copyright: {selectedBible.Copyright}");
if (string.IsNullOrWhiteSpace(selectedBible.Copyright))
{
    throw new InvalidDataException(
        "API.Bible did not return the required copyright notice. Confirm the edition metadata before importing content.");
}

if (!options.Force
    && TryRefreshReviewOnly(outputPath, selectedBible, out var nextReview))
{
    Console.WriteLine($"Source metadata is unchanged. Next review: {nextReview:yyyy-MM-dd}.");
    Console.WriteLine("Only 1 API call was used.");
    return;
}

var referencePath = FindRepositoryFile("asv.json")
    ?? throw new FileNotFoundException("Could not locate asv.json, which is used only as the canonical verse-reference template.");
var references = LoadReferenceVerses(referencePath);
var booksResponse = await GetJsonAsync(
    client,
    $"v1/bibles/{Uri.EscapeDataString(selectedBible.Id)}/books?include-chapters=true");
var books = ParseBooks(booksResponse);
var batches = BuildBatches(references, books, options.BatchSize);

var importDirectory = Path.GetDirectoryName(outputPath)!;
var checkpointDirectory = Path.Combine(importDirectory, ".api-bible-import", selectedBible.Id);
Directory.CreateDirectory(checkpointDirectory);
Console.WriteLine($"Planned calls: 2 metadata calls + {batches.Count} passage calls = {batches.Count + 2} total.");
Console.WriteLine($"Completed checkpoint batches will be reused from {checkpointDirectory}");

var importedById = new Dictionary<string, ImportedVerse>(StringComparer.OrdinalIgnoreCase);
for (var index = 0; index < batches.Count; index++)
{
    var batch = batches[index];
    var checkpointPath = Path.Combine(checkpointDirectory, $"{index + 1:D4}-{batch.BookId}.json");
    List<ImportedVerse> imported;
    if (File.Exists(checkpointPath))
    {
        imported = JsonSerializer.Deserialize<List<ImportedVerse>>(await File.ReadAllTextAsync(checkpointPath), JsonOptions.Default)
            ?? [];
        Console.WriteLine($"[{index + 1}/{batches.Count}] reused {batch.PassageId}");
    }
    else
    {
        var requestPath = $"v1/bibles/{Uri.EscapeDataString(selectedBible.Id)}/passages/{batch.PassageId}" +
                          "?content-type=json&include-notes=false&include-titles=false" +
                          "&include-chapter-numbers=false&include-verse-numbers=true&include-verse-spans=false&fums-version=3";
        var passageResponse = await GetJsonAsync(client, requestPath);
        imported = ParsePassageVerses(passageResponse, batch.ReferenceById);
        if (imported.Count == 0)
        {
            throw new InvalidDataException($"API.Bible returned no parseable verses for {batch.PassageId}.");
        }

        await WriteJsonAtomicallyAsync(checkpointPath, imported);
        Console.WriteLine($"[{index + 1}/{batches.Count}] downloaded {batch.PassageId} ({imported.Count} verses)");
    }

    foreach (var verse in imported)
    {
        importedById[verse.Id] = verse;
    }
}

var missing = references.Where(reference => !importedById.ContainsKey(reference.Id)).ToList();
var expectedIds = references.Select(reference => reference.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
var unexpected = importedById.Keys.Where(id => !expectedIds.Contains(id)).ToList();
if (missing.Count > 0 || unexpected.Count > 0)
{
    throw new InvalidDataException(
        $"Import validation failed. Missing: {missing.Count}; unexpected: {unexpected.Count}. " +
        $"First missing: {missing.FirstOrDefault()?.Id ?? "none"}. Checkpoints were retained for diagnosis/resume.");
}

var now = DateTimeOffset.UtcNow;
var output = new BibleOutput
{
    Metadata = new BibleOutputMetadata
    {
        Name = selectedBible.Name,
        ShortName = "NASB1995",
        Module = "nasb1995",
        Year = "1995",
        Publisher = "The Lockman Foundation",
        Owner = "The Lockman Foundation",
        Description = selectedBible.Description,
        CopyrightStatement = selectedBible.Copyright,
        Source = "API.Bible",
        SourceBibleId = selectedBible.Id,
        SourceUpdatedAt = selectedBible.UpdatedAt,
        ImportedAtUtc = now,
        LastCheckedAtUtc = now,
        RefreshDueAtUtc = now.AddDays(30)
    },
    Verses = references.Select(reference =>
    {
        var imported = importedById[reference.Id];
        return new BibleOutputVerse
        {
            BookName = reference.BookName,
            Book = reference.Book,
            Chapter = reference.Chapter,
            Verse = reference.Verse,
            Text = NormalizeVerseText(imported.Text)
        };
    }).ToList()
};

Directory.CreateDirectory(importDirectory);
await WriteJsonAtomicallyAsync(outputPath, output);
Directory.Delete(Path.Combine(importDirectory, ".api-bible-import"), recursive: true);
Console.WriteLine($"Imported {output.Verses.Count:N0} verses with {batches.Count + 2} API calls.");
Console.WriteLine(outputPath);
Console.WriteLine("Restart Bible Study Studio to load NASB 1995.");

static async Task<JsonDocument> GetJsonAsync(HttpClient client, string requestPath)
{
    using var response = await client.GetAsync(requestPath);
    var body = await response.Content.ReadAsStringAsync();
    if (!response.IsSuccessStatusCode)
    {
        throw new HttpRequestException(
            $"API.Bible returned {(int)response.StatusCode} {response.StatusCode} for {requestPath}: {Trim(body, 1000)}",
            null,
            response.StatusCode);
    }

    return JsonDocument.Parse(body);
}

static List<ApiBible> ParseBibles(JsonDocument document)
{
    return document.RootElement.GetProperty("data").EnumerateArray().Select(element => new ApiBible(
        element.GetProperty("id").GetString() ?? string.Empty,
        GetString(element, "abbreviation"),
        GetString(element, "name"),
        GetString(element, "description"),
        GetString(element, "copyright"),
        GetString(element, "updatedAt"))).ToList();
}

static List<ApiBook> ParseBooks(JsonDocument document)
{
    return document.RootElement.GetProperty("data").EnumerateArray().Select((element, index) => new ApiBook(
        element.GetProperty("id").GetString() ?? string.Empty,
        GetString(element, "name"),
        index + 1)).ToList();
}

static ApiBible SelectNasb1995Bible(IReadOnlyList<ApiBible> bibles, string? requestedId)
{
    if (!string.IsNullOrWhiteSpace(requestedId))
    {
        return bibles.FirstOrDefault(bible => string.Equals(bible.Id, requestedId, StringComparison.OrdinalIgnoreCase))
            ?? throw new InvalidOperationException($"Bible ID '{requestedId}' is not authorized for this API key.");
    }

    var candidates = bibles.Where(bible =>
        Contains(bible.Abbreviation, "NASB") || Contains(bible.Name, "New American Standard")).ToList();
    var dated = candidates.Where(bible =>
        Contains(bible.Abbreviation, "1995") || Contains(bible.Name, "1995") || Contains(bible.Description, "1995")).ToList();
    if (dated.Count == 1)
    {
        return dated[0];
    }

    if (candidates.Count == 1 && !Contains(candidates[0].Name, "2020") && !Contains(candidates[0].Description, "2020"))
    {
        return candidates[0];
    }

    Console.WriteLine("NASB candidates authorized for this key:");
    PrintBibles(candidates);
    throw new InvalidOperationException(
        "NASB 1995 could not be selected unambiguously. Rerun with --bible-id followed by the exact authorized ID.");
}

static void PrintBibles(IEnumerable<ApiBible> bibles)
{
    foreach (var bible in bibles.OrderBy(bible => bible.Name))
    {
        Console.WriteLine($"{bible.Id} | {bible.Abbreviation} | {bible.Name} | updated {bible.UpdatedAt}");
    }
}

static List<ReferenceVerse> LoadReferenceVerses(string path)
{
    using var stream = File.OpenRead(path);
    using var document = JsonDocument.Parse(stream);
    return document.RootElement.GetProperty("verses").EnumerateArray().Select(element =>
    {
        var bookName = element.GetProperty("book_name").GetString() ?? string.Empty;
        var book = element.GetProperty("book").GetInt32();
        var chapter = element.GetProperty("chapter").GetInt32();
        var verse = element.GetProperty("verse").GetInt32();
        return new ReferenceVerse(string.Empty, bookName, book, chapter, verse);
    }).ToList();
}

static List<PassageBatch> BuildBatches(
    List<ReferenceVerse> rawReferences,
    IReadOnlyList<ApiBook> books,
    int batchSize)
{
    var references = new List<ReferenceVerse>(rawReferences.Count);
    foreach (var reference in rawReferences)
    {
        var book = books.FirstOrDefault(candidate =>
            candidate.Order == reference.Book || NamesEquivalent(candidate.Name, reference.BookName));
        if (book is null)
        {
            throw new InvalidDataException($"API.Bible did not return a matching book for {reference.BookName}.");
        }

        references.Add(reference with { Id = $"{book.Id}.{reference.Chapter}.{reference.Verse}" });
    }

    rawReferences.Clear();
    rawReferences.AddRange(references);

    var batches = new List<PassageBatch>();
    foreach (var bookGroup in references.GroupBy(reference => reference.Book).OrderBy(group => group.Key))
    {
        foreach (var chunk in bookGroup.Chunk(batchSize))
        {
            var list = chunk.ToList();
            batches.Add(new PassageBatch(
                list[0].Id.Split('.')[0],
                $"{list[0].Id}-{list[^1].Id}",
                list.ToDictionary(reference => reference.Id, StringComparer.OrdinalIgnoreCase)));
        }
    }

    return batches;
}

static List<ImportedVerse> ParsePassageVerses(
    JsonDocument document,
    IReadOnlyDictionary<string, ReferenceVerse> expected)
{
    var content = document.RootElement.GetProperty("data").GetProperty("content");
    var textById = new Dictionary<string, StringBuilder>(StringComparer.OrdinalIgnoreCase);
    string? currentVerseId = null;
    WalkContent(content, expected, textById, ref currentVerseId, insideVerseMarker: false);
    return textById
        .Where(pair => expected.ContainsKey(pair.Key))
        .Select(pair => new ImportedVerse(pair.Key, pair.Value.ToString()))
        .ToList();
}

static void WalkContent(
    JsonElement element,
    IReadOnlyDictionary<string, ReferenceVerse> expected,
    Dictionary<string, StringBuilder> textById,
    ref string? currentVerseId,
    bool insideVerseMarker)
{
    if (element.ValueKind == JsonValueKind.Array)
    {
        foreach (var item in element.EnumerateArray())
        {
            WalkContent(item, expected, textById, ref currentVerseId, insideVerseMarker);
        }
        return;
    }

    if (element.ValueKind != JsonValueKind.Object)
    {
        return;
    }

    var type = GetString(element, "type");
    var name = GetString(element, "name");
    var isVerseMarker = string.Equals(type, "tag", StringComparison.OrdinalIgnoreCase)
                        && string.Equals(name, "verse", StringComparison.OrdinalIgnoreCase);
    if (element.TryGetProperty("attrs", out var attrs) && attrs.ValueKind == JsonValueKind.Object)
    {
        var verseId = GetString(attrs, "verseId");
        if (!string.IsNullOrWhiteSpace(verseId) && expected.ContainsKey(verseId))
        {
            currentVerseId = verseId;
        }
        else if (isVerseMarker)
        {
            var sid = GetString(attrs, "sid").Replace(' ', '.').Replace(':', '.');
            if (expected.ContainsKey(sid))
            {
                currentVerseId = sid;
            }
        }
    }

    if (string.Equals(type, "text", StringComparison.OrdinalIgnoreCase)
        && !insideVerseMarker
        && currentVerseId is not null
        && expected.ContainsKey(currentVerseId))
    {
        var text = GetString(element, "text");
        if (!string.IsNullOrEmpty(text))
        {
            if (!textById.TryGetValue(currentVerseId, out var builder))
            {
                builder = new StringBuilder();
                textById[currentVerseId] = builder;
            }
            AppendTextChunk(builder, text);
        }
    }

    if (element.TryGetProperty("items", out var items))
    {
        WalkContent(items, expected, textById, ref currentVerseId, insideVerseMarker || isVerseMarker);
    }
}

static bool TryReadFreshImport(string path, out DateTimeOffset freshUntil)
{
    freshUntil = default;
    try
    {
        using var stream = File.OpenRead(path);
        using var document = JsonDocument.Parse(stream);
        var metadata = document.RootElement.GetProperty("metadata");
        return metadata.TryGetProperty("refresh_due_at_utc", out var value)
               && DateTimeOffset.TryParse(value.GetString(), out freshUntil)
               && freshUntil > DateTimeOffset.UtcNow;
    }
    catch
    {
        return false;
    }
}

static bool TryRefreshReviewOnly(string path, ApiBible bible, out DateTimeOffset nextReview)
{
    nextReview = DateTimeOffset.UtcNow.AddDays(30);
    try
    {
        var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject();
        var metadata = root?["metadata"]?.AsObject();
        if (root is null || metadata is null
            || !string.Equals(metadata["source_bible_id"]?.GetValue<string>(), bible.Id, StringComparison.OrdinalIgnoreCase)
            || !string.Equals(metadata["source_updated_at"]?.GetValue<string>(), bible.UpdatedAt, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        metadata["last_checked_at_utc"] = DateTimeOffset.UtcNow;
        metadata["refresh_due_at_utc"] = nextReview;
        File.WriteAllText(path, root.ToJsonString(JsonOptions.Default));
        return true;
    }
    catch
    {
        return false;
    }
}

static async Task WriteJsonAtomicallyAsync<T>(string path, T value)
{
    Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    var temporaryPath = path + ".tmp";
    await File.WriteAllTextAsync(temporaryPath, JsonSerializer.Serialize(value, JsonOptions.Default));
    File.Move(temporaryPath, path, overwrite: true);
}

static void AppendTextChunk(StringBuilder builder, string text)
{
    if (builder.Length > 0 && text.Length > 0 && ShouldSeparateTextChunks(builder[^1], text[0]))
    {
        builder.Append(' ');
    }
    builder.Append(text);
}

static bool ShouldSeparateTextChunks(char previous, char next)
{
    if (char.IsWhiteSpace(previous) || char.IsWhiteSpace(next))
    {
        return false;
    }

    const string openingCharacters = "([{“‘'/*-–—";
    const string closingCharacters = ".,;:!?)]}”’'/%-–—";
    return !openingCharacters.Contains(previous) && !closingCharacters.Contains(next);
}

static string NormalizeVerseText(string text)
{
    var normalized = WebUtility.HtmlDecode(
        string.Join(" ", text.Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries))).Trim();
    normalized = Regex.Replace(normalized, @"(?<=[,;:!?])(?=[\p{L}“‘])", " ");
    normalized = Regex.Replace(normalized, @"(?<=\.)(?=[\p{Lu}“‘])", " ");
    normalized = Regex.Replace(normalized, "(?<=[”\"])(?=\\p{L})", " ");
    normalized = Regex.Replace(normalized, @"(?<=\p{Ll})(?=\p{Lu})", " ");
    normalized = Regex.Replace(normalized, @"(?<=\p{Lu})(?=\p{Lu}\p{Ll})", " ");
    return normalized;
}

static void RepairExistingImport(string path)
{
    if (!File.Exists(path))
    {
        throw new FileNotFoundException("The Bible import file was not found.", path);
    }

    var root = JsonNode.Parse(File.ReadAllText(path))?.AsObject()
        ?? throw new InvalidOperationException("The Bible import file is not valid JSON.");
    var verses = root["verses"]?.AsArray()
        ?? throw new InvalidOperationException("The Bible import file does not contain a verses array.");
    var repairedCount = 0;
    foreach (var verseNode in verses)
    {
        var verse = verseNode?.AsObject();
        var original = verse?["text"]?.GetValue<string>();
        if (verse is null || string.IsNullOrEmpty(original))
        {
            continue;
        }

        var repaired = NormalizeVerseText(original);
        if (string.Equals(original, repaired, StringComparison.Ordinal))
        {
            continue;
        }

        verse["text"] = repaired;
        repairedCount++;
    }

    if (root["metadata"] is JsonObject metadata)
    {
        metadata["text_spacing_repaired_at_utc"] = DateTimeOffset.UtcNow;
    }

    var temporaryPath = path + ".tmp";
    File.WriteAllText(temporaryPath, root.ToJsonString(JsonOptions.Default));
    File.Move(temporaryPath, path, overwrite: true);
    Console.WriteLine($"Repaired spacing in {repairedCount:N0} verses without making any API calls.");
    Console.WriteLine(path);
}

static string? FindRepositoryFile(string fileName)
{
    foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
    {
        var cursor = new DirectoryInfo(start);
        while (cursor is not null)
        {
            var candidate = Path.Combine(cursor.FullName, fileName);
            if (File.Exists(candidate))
            {
                return candidate;
            }
            cursor = cursor.Parent;
        }
    }
    return null;
}

static string ReadSecret(string prompt)
{
    Console.Write(prompt);
    var value = new StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter)
        {
            Console.WriteLine();
            return value.ToString();
        }
        if (key.Key == ConsoleKey.Backspace && value.Length > 0)
        {
            value.Length--;
        }
        else if (!char.IsControl(key.KeyChar))
        {
            value.Append(key.KeyChar);
        }
    }
}

static string GetString(JsonElement element, string propertyName) =>
    element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
        ? property.GetString() ?? string.Empty
        : string.Empty;

static bool Contains(string value, string fragment) =>
    value.Contains(fragment, StringComparison.OrdinalIgnoreCase);

static bool NamesEquivalent(string first, string second)
{
    static string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());
    return Normalize(first) == Normalize(second);
}

static string Trim(string value, int maxLength) =>
    value.Length <= maxLength ? value : value[..maxLength] + "...";

record ApiBible(string Id, string Abbreviation, string Name, string Description, string Copyright, string UpdatedAt);
record ApiBook(string Id, string Name, int Order);
record ReferenceVerse(string Id, string BookName, int Book, int Chapter, int Verse);
record ImportedVerse(string Id, string Text);
record PassageBatch(string BookId, string PassageId, IReadOnlyDictionary<string, ReferenceVerse> ReferenceById);

sealed class BibleOutput
{
    [JsonPropertyName("metadata")]
    public BibleOutputMetadata Metadata { get; set; } = new();

    [JsonPropertyName("verses")]
    public List<BibleOutputVerse> Verses { get; set; } = [];
}

sealed class BibleOutputMetadata
{
    [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("shortname")] public string ShortName { get; set; } = string.Empty;
    [JsonPropertyName("module")] public string Module { get; set; } = string.Empty;
    [JsonPropertyName("year")] public string Year { get; set; } = string.Empty;
    [JsonPropertyName("publisher")] public string Publisher { get; set; } = string.Empty;
    [JsonPropertyName("owner")] public string Owner { get; set; } = string.Empty;
    [JsonPropertyName("description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("copyright_statement")] public string CopyrightStatement { get; set; } = string.Empty;
    [JsonPropertyName("source")] public string Source { get; set; } = string.Empty;
    [JsonPropertyName("source_bible_id")] public string SourceBibleId { get; set; } = string.Empty;
    [JsonPropertyName("source_updated_at")] public string SourceUpdatedAt { get; set; } = string.Empty;
    [JsonPropertyName("imported_at_utc")] public DateTimeOffset ImportedAtUtc { get; set; }
    [JsonPropertyName("last_checked_at_utc")] public DateTimeOffset LastCheckedAtUtc { get; set; }
    [JsonPropertyName("refresh_due_at_utc")] public DateTimeOffset RefreshDueAtUtc { get; set; }
}

sealed class BibleOutputVerse
{
    [JsonPropertyName("book_name")] public string BookName { get; set; } = string.Empty;
    [JsonPropertyName("book")] public int Book { get; set; }
    [JsonPropertyName("chapter")] public int Chapter { get; set; }
    [JsonPropertyName("verse")] public int Verse { get; set; }
    [JsonPropertyName("text")] public string Text { get; set; } = string.Empty;
}

sealed record ImportOptions(
    string Endpoint,
    string? BibleId,
    string? OutputPath,
    int BatchSize,
    bool ListOnly,
    bool Force)
{
    public static ImportOptions Parse(string[] args)
    {
        var endpoint = "https://rest.api.bible";
        string? bibleId = null;
        string? output = null;
        var batchSize = 190;
        var listOnly = false;
        var force = false;
        for (var index = 0; index < args.Length; index++)
        {
            switch (args[index])
            {
                case "--endpoint": endpoint = RequireValue(args, ref index); break;
                case "--bible-id": bibleId = RequireValue(args, ref index); break;
                case "--output": output = RequireValue(args, ref index); break;
                case "--batch-size": batchSize = int.Parse(RequireValue(args, ref index)); break;
                case "--list": listOnly = true; break;
                case "--force": force = true; break;
                default: throw new ArgumentException($"Unknown option: {args[index]}");
            }
        }

        if (batchSize is < 1 or > 190)
        {
            throw new ArgumentOutOfRangeException(nameof(batchSize), "Batch size must be between 1 and 190.");
        }
        return new ImportOptions(endpoint, bibleId, output, batchSize, listOnly, force);
    }

    private static string RequireValue(string[] args, ref int index)
    {
        if (++index >= args.Length)
        {
            throw new ArgumentException("An option value is missing.");
        }
        return args[index];
    }
}

static class JsonOptions
{
    public static readonly JsonSerializerOptions Default = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };
}
