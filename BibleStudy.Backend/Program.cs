using System.Net;
using System.Net.Http.Headers;
using System.Net.Sockets;
using System.Diagnostics;
using System.Text;
using System.Text.Json;

const string DefaultBackendUrl = "http://localhost:5055";

if (await BackendStartup.ExistingBackendIsRunningAsync(DefaultBackendUrl))
{
    Console.WriteLine($"Bible Study Backend is already running at {DefaultBackendUrl}.");
    return;
}

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(builder.Configuration["Backend:Url"] ?? DefaultBackendUrl);

builder.Services.AddSingleton<StudySessionService>();

var app = builder.Build();

_ = Task.Run(() => OllamaStartup.EnsureRunningAsync(app.Configuration));

app.MapGet("/", () => Results.Ok(new
{
    name = "Bible Study Backend",
    status = "running"
}));

app.MapPost("/api/study-sessions/end", async (
    EndStudySessionRequest request,
    StudySessionService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (string.IsNullOrWhiteSpace(request.StudyName))
        {
            return Results.BadRequest(new { error = "StudyName is required." });
        }

        var response = await service.EndStudySessionAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        BackendLog.Write(ex);
        return Results.Problem(
            title: "End study session failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.MapPost("/api/notifications/cancel", async (
    CancelNotificationsRequest request,
    StudySessionService service,
    CancellationToken cancellationToken) =>
{
    try
    {
        if (request.SequenceIds.Count == 0)
        {
            return Results.BadRequest(new { error = "At least one SequenceId is required." });
        }

        var response = await service.CancelNotificationsAsync(request, cancellationToken);
        return Results.Ok(response);
    }
    catch (Exception ex)
    {
        BackendLog.Write(ex);
        return Results.Problem(
            title: "Cancel scheduled notifications failed",
            detail: ex.Message,
            statusCode: StatusCodes.Status500InternalServerError);
    }
});

app.Run();

public sealed class StudySessionService
{
    private static readonly HttpClient HttpClient = new();
    private static readonly HttpClient NtfyHttpClient = new(CreateIPv4OnlyHandler());

    private readonly IConfiguration _configuration;

    public StudySessionService(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async Task<EndStudySessionResponse> EndStudySessionAsync(
        EndStudySessionRequest request,
        CancellationToken cancellationToken)
    {
        var notifications = new List<NotificationDispatchResult>();
        var sessionId = CreateSessionId(request.StudyName);
        var overview = string.Empty;

        if (request.OverviewEnabled)
        {
            overview = await TryCreateAiStudyOverviewAsync(request, cancellationToken)
                ?? CreateLocalStudyOverview(request);

            notifications.Add(await TryPublishNtfyAsync(
                title: $"Study overview: {request.StudyName}",
                message: CreateOverviewMessage(request, overview),
                delay: null,
                sequenceId: $"{sessionId}-overview",
                cancellationToken));
        }

        foreach (var reminder in await CreateReminderScheduleAsync(request, cancellationToken))
        {
            notifications.Add(await TryPublishNtfyAsync(
                title: reminder.Title,
                message: reminder.Message,
                delay: reminder.Delay,
                sequenceId: $"{sessionId}-{reminder.Key}",
                cancellationToken));
        }

        return new EndStudySessionResponse(overview, notifications);
    }

    public async Task<CancelNotificationsResponse> CancelNotificationsAsync(
        CancelNotificationsRequest request,
        CancellationToken cancellationToken)
    {
        var results = new List<NotificationCancelResult>();

        foreach (var sequenceId in request.SequenceIds
                     .Where(sequenceId => !string.IsNullOrWhiteSpace(sequenceId))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            try
            {
                await CancelNtfyAsync(sequenceId, cancellationToken);
                results.Add(new NotificationCancelResult(sequenceId, true, null));
            }
            catch (Exception ex)
            {
                BackendLog.Write(ex);
                results.Add(new NotificationCancelResult(sequenceId, false, FormatExceptionMessage(ex)));
            }
        }

        return new CancelNotificationsResponse(results);
    }

    private async Task<NotificationDispatchResult> TryPublishNtfyAsync(
        string title,
        string message,
        string? delay,
        string sequenceId,
        CancellationToken cancellationToken)
    {
        try
        {
            return await PublishNtfyAsync(title, message, delay, sequenceId, cancellationToken);
        }
        catch (Exception ex)
        {
            BackendLog.Write(ex);
            return new NotificationDispatchResult(title, delay ?? "now", sequenceId, false, FormatExceptionMessage(ex));
        }
    }

    private async Task<string?> TryCreateAiStudyOverviewAsync(
        EndStudySessionRequest request,
        CancellationToken cancellationToken)
    {
        var endpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1/responses";
        var apiKey = _configuration["Ollama:ApiKey"] ?? "ollama";
        var model = _configuration["Ollama:Model"] ?? "llama3.2:latest";

        var prompt = new StringBuilder();
        prompt.AppendLine("Create a concise, pastoral Bible study overview for a phone notification.");
        prompt.AppendLine("Use the user's overview instruction, passage reference, and selected scripture text. Keep it under 90 words.");
        prompt.AppendLine("When scripture text is provided, quote from it when useful instead of guessing the wording.");
        prompt.AppendLine("Do not add fluff, an intro, an outro, acknowledgments, or transition phrases. Start directly with what the user asked for.");
        prompt.AppendLine("Return only the notification text. Do not include reasoning or labels.");
        prompt.AppendLine();
        prompt.AppendLine($"Overview instruction: {EmptyFallback(request.OverviewPrompt, "Summarize this study and give one clear next step.")}");
        prompt.AppendLine($"Passage: {request.PassageReference}");
        prompt.AppendLine($"Selected scripture text: {EmptyFallback(request.ScriptureText, "No scripture text selected.")}");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model,
                input = prompt.ToString(),
                max_output_tokens = 180,
                reasoning = new
                {
                    effort = "none"
                }
            });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractResponseText(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<string?> TryCreateAiReminderAsync(
        EndStudySessionRequest request,
        StudyReminderRequest reminder,
        CancellationToken cancellationToken)
    {
        var endpoint = _configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1/responses";
        var apiKey = _configuration["Ollama:ApiKey"] ?? "ollama";
        var model = _configuration["Ollama:Model"] ?? "llama3.2:latest";

        var prompt = new StringBuilder();
        prompt.AppendLine("Create a fresh Bible study follow-up notification.");
        prompt.AppendLine("Use the user's reminder instruction, passage reference, and selected scripture text.");
        prompt.AppendLine("When scripture text is provided, quote from it when useful instead of guessing the wording.");
        prompt.AppendLine("Do not copy the instruction verbatim. Write a new pastoral, specific message under 70 words.");
        prompt.AppendLine("Do not add fluff, an intro, an outro, acknowledgments, or transition phrases. Start directly with what the user asked for.");
        prompt.AppendLine("Return only the notification text. Do not include labels.");
        prompt.AppendLine();
        prompt.AppendLine($"Reminder instruction: {EmptyFallback(reminder.Instruction, "Bring me back to this study.")}");
        prompt.AppendLine($"Study name: {EmptyFallback(request.StudyName, "Bible study")}");
        prompt.AppendLine($"Passage: {EmptyFallback(request.PassageReference, "No passage selected")}");
        prompt.AppendLine($"Selected scripture text: {EmptyFallback(request.ScriptureText, "No scripture text selected.")}");

        try
        {
            var payload = JsonSerializer.Serialize(new
            {
                model,
                input = prompt.ToString(),
                max_output_tokens = 160,
                reasoning = new
                {
                    effort = "none"
                }
            });

            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, endpoint)
            {
                Content = new StringContent(payload, Encoding.UTF8, "application/json")
            };
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);

            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return ExtractResponseText(json);
        }
        catch
        {
            return null;
        }
    }

    private async Task<NotificationDispatchResult> PublishNtfyAsync(
        string title,
        string message,
        string? delay,
        string sequenceId,
        CancellationToken cancellationToken)
    {
        var topicUrl = _configuration["Ntfy:TopicUrl"] ?? "https://ntfy.sh/Jake_Alerts";
        var notificationUrl = CreateNtfySequenceUrl(topicUrl, sequenceId);

        using var request = new HttpRequestMessage(HttpMethod.Post, notificationUrl)
        {
            Content = new StringContent(message, Encoding.UTF8, "text/plain")
        };
        request.Headers.TryAddWithoutValidation("Title", title);
        request.Headers.TryAddWithoutValidation("Priority", "default");
        request.Headers.TryAddWithoutValidation("Tags", "books");

        if (!string.IsNullOrWhiteSpace(delay))
        {
            request.Headers.TryAddWithoutValidation("In", delay);
        }

        BackendLog.WriteInfo($"Publishing notification '{title}' with delay '{delay ?? "now"}' and sequence '{sequenceId}'.");

        using var response = await NtfyHttpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        return new NotificationDispatchResult(title, delay ?? "now", sequenceId, true, null);
    }

    private async Task CancelNtfyAsync(string sequenceId, CancellationToken cancellationToken)
    {
        var topicUrl = _configuration["Ntfy:TopicUrl"] ?? "https://ntfy.sh/Jake_Alerts";
        var notificationUrl = CreateNtfySequenceUrl(topicUrl, sequenceId);

        BackendLog.WriteInfo($"Canceling notification sequence '{sequenceId}'.");

        using var response = await NtfyHttpClient.DeleteAsync(notificationUrl, cancellationToken);
        response.EnsureSuccessStatusCode();
    }

    private static string CreateNtfySequenceUrl(string topicUrl, string sequenceId)
    {
        return $"{topicUrl.TrimEnd('/')}/{Uri.EscapeDataString(sequenceId)}";
    }

    private static SocketsHttpHandler CreateIPv4OnlyHandler()
    {
        return new SocketsHttpHandler
        {
            ConnectCallback = async (context, cancellationToken) =>
            {
                var addresses = await Dns.GetHostAddressesAsync(context.DnsEndPoint.Host, AddressFamily.InterNetwork, cancellationToken);
                var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp)
                {
                    NoDelay = true
                };

                try
                {
                    await socket.ConnectAsync(addresses, context.DnsEndPoint.Port, cancellationToken);
                    return new NetworkStream(socket, ownsSocket: true);
                }
                catch
                {
                    socket.Dispose();
                    throw;
                }
            }
        };
    }

    private async Task<List<ReminderDraft>> CreateReminderScheduleAsync(
        EndStudySessionRequest request,
        CancellationToken cancellationToken)
    {
        var reminders = new List<ReminderDraft>();
        BackendLog.WriteInfo(
            $"Reminder request received. Enabled={request.RemindersEnabled}; Count={request.Reminders.Count}; Delays={string.Join(", ", request.Reminders.Select(reminder => reminder.Delay))}");

        if (!request.RemindersEnabled)
        {
            return reminders;
        }

        foreach (var reminder in request.Reminders.Where(reminder =>
                     !string.IsNullOrWhiteSpace(reminder.Instruction)
                     && !string.IsNullOrWhiteSpace(reminder.Delay)))
        {
            var message = await TryCreateAiReminderAsync(request, reminder, cancellationToken)
                ?? CreateLocalReminderMessage(request, reminder);

            reminders.Add(new ReminderDraft(
                string.IsNullOrWhiteSpace(reminder.Key) ? "custom" : reminder.Key,
                string.IsNullOrWhiteSpace(reminder.Title) ? "Study reminder" : reminder.Title,
                message,
                reminder.Delay));
        }

        return reminders;
    }

    private static string CreateOverviewMessage(EndStudySessionRequest request, string overview)
    {
        var message = new StringBuilder();
        message.AppendLine("Study ended for now.");
        message.AppendLine();
        message.AppendLine($"Passage: {EmptyFallback(request.PassageReference, "No passage selected")}");
        message.AppendLine($"Overview: {overview}");

        return message.ToString();
    }

    private static string CreateLocalStudyOverview(EndStudySessionRequest request)
    {
        var reference = EmptyFallback(request.PassageReference, "today's passage");
        var scripture = NormalizeWhitespace(request.ScriptureText);
        var focus = string.IsNullOrWhiteSpace(scripture)
            ? "Return with fresh attention."
            : $"Remember: {TrimForNotification(scripture, 180)}";

        return $"{reference}. {focus}";
    }

    private static string CreateLocalReminderMessage(EndStudySessionRequest request, StudyReminderRequest reminder)
    {
        var instruction = EmptyFallback(reminder.Instruction, "Return to this Bible study.");
        var reference = EmptyFallback(request.PassageReference, "today's passage");

        return $"{TrimForNotification(instruction, 180)} Passage: {reference}.";
    }

    private static string? ExtractResponseText(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.TryGetProperty("output_text", out var outputText)
            && outputText.ValueKind == JsonValueKind.String)
        {
            return outputText.GetString()?.Trim();
        }

        if (!document.RootElement.TryGetProperty("output", out var output)
            || output.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var outputItem in output.EnumerateArray())
        {
            if (!outputItem.TryGetProperty("content", out var content)
                || content.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            foreach (var contentItem in content.EnumerateArray())
            {
                if (contentItem.TryGetProperty("text", out var text)
                    && text.ValueKind == JsonValueKind.String)
                {
                    return text.GetString()?.Trim();
                }
            }
        }

        return null;
    }

    private static string CreateSessionId(string studyName)
    {
        var normalized = new string(studyName
            .Where(char.IsLetterOrDigit)
            .Take(28)
            .ToArray());

        return $"{(string.IsNullOrWhiteSpace(normalized) ? "study" : normalized)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmss}";
    }

    private static string EmptyFallback(string? value, string fallback)
    {
        return string.IsNullOrWhiteSpace(value)
            ? fallback
            : value.Trim();
    }

    private static string NormalizeWhitespace(string? text)
    {
        return string.Join(" ", (text ?? string.Empty).Split([' ', '\r', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries));
    }

    private static string TrimForNotification(string text, int maxLength)
    {
        var compact = NormalizeWhitespace(text);
        return compact.Length <= maxLength
            ? compact
            : $"{compact[..Math.Max(0, maxLength - 3)]}...";
    }

    private static string FormatExceptionMessage(Exception exception)
    {
        var messages = new List<string>();
        var cursor = exception;
        while (cursor is not null)
        {
            messages.Add(cursor.Message);
            cursor = cursor.InnerException;
        }

        return string.Join(" ", messages.Distinct());
    }
}

public sealed class EndStudySessionRequest
{
    public string StudyName { get; init; } = string.Empty;

    public string PassageReference { get; init; } = string.Empty;

    public string ScriptureText { get; init; } = string.Empty;

    public bool OverviewEnabled { get; init; } = true;

    public string OverviewPrompt { get; init; } = string.Empty;

    public bool RemindersEnabled { get; init; } = true;

    public IReadOnlyList<StudyReminderRequest> Reminders { get; init; } = [];
}

public sealed record StudyReminderRequest(
    string Key,
    string Title,
    string Instruction,
    string Delay);

public sealed record EndStudySessionResponse(
    string Overview,
    IReadOnlyList<NotificationDispatchResult> Notifications);

public sealed record CancelNotificationsRequest(
    IReadOnlyList<string> SequenceIds);

public sealed record CancelNotificationsResponse(
    IReadOnlyList<NotificationCancelResult> Notifications);

public sealed record NotificationDispatchResult(
    string Title,
    string Delivery,
    string SequenceId,
    bool Sent,
    string? Error);

public sealed record NotificationCancelResult(
    string SequenceId,
    bool Canceled,
    string? Error);

public sealed record ReminderDraft(
    string Key,
    string Title,
    string Message,
    string Delay);

public static class BackendStartup
{
    public static async Task<bool> ExistingBackendIsRunningAsync(string backendUrl)
    {
        try
        {
            using var client = new HttpClient
            {
                Timeout = TimeSpan.FromMilliseconds(750)
            };

            using var response = await client.GetAsync(backendUrl);
            if (!response.IsSuccessStatusCode)
            {
                return false;
            }

            var json = await response.Content.ReadAsStringAsync();
            return json.Contains("Bible Study Backend", StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }
}

public static class OllamaStartup
{
    private static readonly HttpClient StartupHttpClient = new()
    {
        Timeout = TimeSpan.FromMilliseconds(900)
    };

    public static async Task EnsureRunningAsync(IConfiguration configuration)
    {
        if (!bool.TryParse(configuration["Ollama:StartOnBackendStartup"], out var startOnStartup))
        {
            startOnStartup = true;
        }

        if (!startOnStartup)
        {
            return;
        }

        var endpoint = configuration["Ollama:Endpoint"] ?? "http://localhost:11434/v1/responses";
        var baseUri = GetOllamaBaseUri(endpoint);
        if (await IsOllamaRunningAsync(baseUri))
        {
            return;
        }

        TryStartOllama(configuration);

        for (var attempt = 0; attempt < 20; attempt++)
        {
            await Task.Delay(500);
            if (await IsOllamaRunningAsync(baseUri))
            {
                return;
            }
        }

        BackendLog.Write(new InvalidOperationException(
            $"Ollama did not respond at {baseUri} after backend startup attempted to launch it."));
    }

    private static void TryStartOllama(IConfiguration configuration)
    {
        try
        {
            var command = configuration["Ollama:StartCommand"] ?? "ollama";
            var arguments = configuration["Ollama:StartArguments"] ?? "serve";
            Process.Start(new ProcessStartInfo
            {
                FileName = command,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false,
                WindowStyle = ProcessWindowStyle.Hidden
            });
        }
        catch (Exception ex)
        {
            BackendLog.Write(ex);
        }
    }

    private static async Task<bool> IsOllamaRunningAsync(Uri baseUri)
    {
        try
        {
            var healthUri = new Uri(baseUri, "/api/tags");
            using var response = await StartupHttpClient.GetAsync(healthUri);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    private static Uri GetOllamaBaseUri(string endpoint)
    {
        if (!Uri.TryCreate(endpoint, UriKind.Absolute, out var endpointUri))
        {
            return new Uri("http://localhost:11434/");
        }

        return new Uri($"{endpointUri.Scheme}://{endpointUri.Authority}/");
    }
}

public static class BackendLog
{
    private static readonly object SyncRoot = new();

    public static void WriteInfo(string message)
    {
        WriteEntry($"[{DateTimeOffset.Now:O}] INFO: {message}");
    }

    public static void Write(Exception exception)
    {
        try
        {
            var entry = new StringBuilder()
                .AppendLine($"[{DateTimeOffset.Now:O}] {exception.GetType().FullName}: {exception.Message}")
                .AppendLine(exception.ToString())
                .AppendLine()
                .ToString();
            WriteEntry(entry);
        }
        catch
        {
        }
    }

    private static void WriteEntry(string entry)
    {
        try
        {
            var directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Bible Study Studio",
                "Backend");
            Directory.CreateDirectory(directory);

            var logPath = Path.Combine(directory, "backend.log");
            lock (SyncRoot)
            {
                File.AppendAllText(logPath, entry.EndsWith(Environment.NewLine)
                    ? entry
                    : $"{entry}{Environment.NewLine}");
            }
        }
        catch
        {
        }
    }
}
