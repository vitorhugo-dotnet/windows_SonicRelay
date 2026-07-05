using System.Text.Json;

namespace SonicRelay.Windows.Core.Diagnostics;

public sealed record DiagnosticEvent(
    DateTimeOffset Timestamp,
    string Category,
    string Message,
    IReadOnlyDictionary<string, string> Properties);

public sealed class DiagnosticLog : IDisposable
{
    private const int EventLimit = 100;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim writeLock = new(1, 1);
    private readonly List<DiagnosticEvent> recentEvents = [];

    public DiagnosticLog(string? directory = null)
    {
        var root = directory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SonicRelay", "WindowsPublisher", "logs");
        LogPath = Path.Combine(root, $"publisher-{DateTime.UtcNow:yyyyMMdd}.jsonl");
    }

    public string LogPath { get; }
    public IReadOnlyList<DiagnosticEvent> RecentEvents
    {
        get { lock (recentEvents) return recentEvents.ToArray(); }
    }

    public async Task WriteAsync(
        string category,
        string message,
        IReadOnlyDictionary<string, string>? properties = null,
        CancellationToken cancellationToken = default)
    {
        var safeProperties = (properties ?? new Dictionary<string, string>())
            .ToDictionary(
                pair => DiagnosticRedactor.Redact(pair.Key),
                pair => DiagnosticRedactor.IsSensitiveKey(pair.Key) ? "[REDACTED]" : DiagnosticRedactor.Redact(pair.Value));
        var item = new DiagnosticEvent(
            DateTimeOffset.UtcNow,
            DiagnosticRedactor.Redact(category),
            DiagnosticRedactor.Redact(message),
            safeProperties);

        lock (recentEvents)
        {
            recentEvents.Add(item);
            if (recentEvents.Count > EventLimit) recentEvents.RemoveRange(0, recentEvents.Count - EventLimit);
        }

        await writeLock.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(LogPath)!);
            await File.AppendAllTextAsync(LogPath, JsonSerializer.Serialize(item, JsonOptions) + Environment.NewLine, cancellationToken);
        }
        finally
        {
            writeLock.Release();
        }
    }

    public void Dispose() => writeLock.Dispose();
}
