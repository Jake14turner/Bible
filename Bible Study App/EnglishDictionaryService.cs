using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;

namespace Bible_Study_App;

internal sealed record EnglishDictionaryEntry(
    string Word,
    string PartOfSpeech,
    IReadOnlyList<string> Definitions,
    IReadOnlyList<string> Synonyms);

internal sealed class EnglishDictionaryService
{
    private sealed record IndexHit(char PartOfSpeech, long Offset);

    private static readonly (char Code, string FileSuffix, string Label)[] PartsOfSpeech =
    [
        ('n', "noun", "noun"),
        ('v', "verb", "verb"),
        ('a', "adj", "adjective"),
        ('r', "adv", "adverb")
    ];

    private readonly object _loadGate = new();
    private readonly Dictionary<string, EnglishDictionaryEntry?> _lookupCache = new(StringComparer.OrdinalIgnoreCase);
    private Dictionary<string, List<IndexHit>>? _index;
    private string? _dictionaryDirectory;

    public void WarmUp()
    {
        lock (_loadGate)
        {
            EnsureIndexLoaded();
        }
    }

    public bool TryLookup(string rawWord, out EnglishDictionaryEntry entry)
    {
        entry = new EnglishDictionaryEntry(string.Empty, string.Empty, [], []);
        var normalized = NormalizeLookupWord(rawWord);
        if (string.IsNullOrWhiteSpace(normalized))
        {
            return false;
        }

        lock (_loadGate)
        {
            if (_lookupCache.TryGetValue(normalized, out var cached))
            {
                if (cached is null)
                {
                    return false;
                }

                entry = cached;
                return true;
            }

            EnsureIndexLoaded();
            if (_index is null || string.IsNullOrWhiteSpace(_dictionaryDirectory))
            {
                _lookupCache[normalized] = null;
                return false;
            }

            foreach (var candidate in GetLookupCandidates(normalized))
            {
                if (!_index.TryGetValue(candidate, out var hits))
                {
                    continue;
                }

                var resolved = ReadEntry(candidate, hits);
                if (resolved is null)
                {
                    continue;
                }

                _lookupCache[normalized] = resolved;
                entry = resolved;
                return true;
            }

            _lookupCache[normalized] = null;
            return false;
        }
    }

    private void EnsureIndexLoaded()
    {
        if (_index is not null)
        {
            return;
        }

        _dictionaryDirectory = FindDictionaryDirectory();
        _index = new Dictionary<string, List<IndexHit>>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(_dictionaryDirectory))
        {
            return;
        }

        foreach (var (code, suffix, _) in PartsOfSpeech)
        {
            var path = Path.Combine(_dictionaryDirectory, $"index.{suffix}");
            if (!File.Exists(path))
            {
                continue;
            }

            foreach (var line in File.ReadLines(path, Encoding.ASCII))
            {
                if (string.IsNullOrWhiteSpace(line) || char.IsWhiteSpace(line[0]))
                {
                    continue;
                }

                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                if (parts.Length < 7
                    || !int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out var synsetCount)
                    || !int.TryParse(parts[3], NumberStyles.Integer, CultureInfo.InvariantCulture, out var pointerCount))
                {
                    continue;
                }

                var offsetStart = 6 + pointerCount;
                if (offsetStart + synsetCount > parts.Length)
                {
                    continue;
                }

                var lemma = parts[0].ToLowerInvariant();
                if (!_index.TryGetValue(lemma, out var hits))
                {
                    hits = [];
                    _index[lemma] = hits;
                }

                for (var index = 0; index < synsetCount; index++)
                {
                    if (long.TryParse(parts[offsetStart + index], NumberStyles.Integer, CultureInfo.InvariantCulture, out var offset))
                    {
                        hits.Add(new IndexHit(code, offset));
                    }
                }
            }
        }
    }

    private EnglishDictionaryEntry? ReadEntry(string lemma, IReadOnlyList<IndexHit> hits)
    {
        var definitions = new List<string>();
        var synonyms = new List<string>();
        var partOfSpeechLabels = new List<string>();

        foreach (var hit in hits.Take(6))
        {
            var part = PartsOfSpeech.FirstOrDefault(candidate => candidate.Code == hit.PartOfSpeech);
            if (string.IsNullOrWhiteSpace(part.FileSuffix))
            {
                continue;
            }

            var line = ReadDataLine(Path.Combine(_dictionaryDirectory!, $"data.{part.FileSuffix}"), hit.Offset);
            if (string.IsNullOrWhiteSpace(line) || !TryParseSynset(line, out var definition, out var synsetWords))
            {
                continue;
            }

            if (!definitions.Contains(definition, StringComparer.OrdinalIgnoreCase))
            {
                definitions.Add(definition);
            }

            if (!partOfSpeechLabels.Contains(part.Label, StringComparer.OrdinalIgnoreCase))
            {
                partOfSpeechLabels.Add(part.Label);
            }

            foreach (var synonym in synsetWords)
            {
                if (!synonym.Equals(lemma, StringComparison.OrdinalIgnoreCase)
                    && !synonyms.Contains(synonym, StringComparer.OrdinalIgnoreCase))
                {
                    synonyms.Add(synonym);
                }
            }
        }

        if (definitions.Count == 0)
        {
            return null;
        }

        return new EnglishDictionaryEntry(
            lemma.Replace('_', ' '),
            string.Join(" · ", partOfSpeechLabels),
            definitions.Take(3).ToList(),
            synonyms.Take(8).ToList());
    }

    private static string? ReadDataLine(string path, long offset)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        stream.Seek(offset, SeekOrigin.Begin);
        using var reader = new StreamReader(stream, Encoding.ASCII, detectEncodingFromByteOrderMarks: false);
        return reader.ReadLine();
    }

    private static bool TryParseSynset(string line, out string definition, out List<string> words)
    {
        definition = string.Empty;
        words = [];
        var glossIndex = line.IndexOf('|');
        if (glossIndex < 0)
        {
            return false;
        }

        var metadata = line[..glossIndex].Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (metadata.Length < 5
            || !int.TryParse(metadata[3], NumberStyles.HexNumber, CultureInfo.InvariantCulture, out var wordCount)
            || metadata.Length < 4 + (wordCount * 2))
        {
            return false;
        }

        for (var index = 0; index < wordCount; index++)
        {
            var word = metadata[4 + (index * 2)].Replace('_', ' ');
            if (!words.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                words.Add(word);
            }
        }

        var gloss = line[(glossIndex + 1)..].Trim();
        definition = Regex.Split(gloss, ";\\s*\\\"", RegexOptions.CultureInvariant)[0].Trim(' ', ';', '.');
        return !string.IsNullOrWhiteSpace(definition);
    }

    private static IEnumerable<string> GetLookupCandidates(string normalized)
    {
        var candidates = new List<string>();
        void Add(string candidate)
        {
            if (candidate.Length > 1 && !candidates.Contains(candidate, StringComparer.OrdinalIgnoreCase))
            {
                candidates.Add(candidate);
            }
        }

        Add(normalized);
        if (normalized.EndsWith("ies", StringComparison.Ordinal) && normalized.Length > 3)
        {
            Add($"{normalized[..^3]}y");
        }
        if (normalized.EndsWith("ied", StringComparison.Ordinal) && normalized.Length > 3)
        {
            Add($"{normalized[..^3]}y");
        }
        if (normalized.EndsWith("ing", StringComparison.Ordinal) && normalized.Length > 4)
        {
            var stem = normalized[..^3];
            Add(stem);
            Add($"{stem}e");
            if (stem.Length > 2 && stem[^1] == stem[^2])
            {
                Add(stem[..^1]);
            }
        }
        if (normalized.EndsWith("ed", StringComparison.Ordinal) && normalized.Length > 3)
        {
            var stem = normalized[..^2];
            Add(stem);
            Add($"{stem}e");
            if (stem.Length > 2 && stem[^1] == stem[^2])
            {
                Add(stem[..^1]);
            }
        }
        if (normalized.EndsWith("es", StringComparison.Ordinal) && normalized.Length > 3)
        {
            Add(normalized[..^2]);
        }
        if (normalized.EndsWith('s') && normalized.Length > 2)
        {
            Add(normalized[..^1]);
        }

        return candidates;
    }

    private static string NormalizeLookupWord(string rawWord)
    {
        var normalized = rawWord.Trim().ToLowerInvariant().Replace('’', '\'');
        normalized = Regex.Replace(normalized, @"^[^a-z]+|[^a-z']+$", string.Empty, RegexOptions.CultureInvariant);
        return normalized.Replace(' ', '_');
    }

    private static string? FindDictionaryDirectory()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "Data", "EnglishDictionary"),
            Path.Combine(AppContext.BaseDirectory, "EnglishDictionary"),
            Path.Combine(Environment.CurrentDirectory, "Data", "EnglishDictionary"),
            Path.Combine(Environment.CurrentDirectory, "Bible Study App", "Data", "EnglishDictionary")
        };
        return candidates.FirstOrDefault(directory => File.Exists(Path.Combine(directory, "index.noun")));
    }
}
