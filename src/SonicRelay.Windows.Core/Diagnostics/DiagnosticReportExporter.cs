using System.Text;

namespace SonicRelay.Windows.Core.Diagnostics;

public sealed class DiagnosticReportExporter
{
    private readonly string directory;

    public DiagnosticReportExporter(string? directory = null) => this.directory = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonicRelay", "WindowsPublisher", "diagnostics");

    public async Task<string> ExportAsync(DiagnosticsSnapshot snapshot, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, $"sonicrelay-diagnostics-{DateTime.UtcNow:yyyyMMdd-HHmmss}.md");
        var temporaryPath = path + ".tmp";
        await File.WriteAllTextAsync(temporaryPath, Render(snapshot), Encoding.UTF8, cancellationToken);
        File.Move(temporaryPath, path, true);
        return path;
    }

    public static string Render(DiagnosticsSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        static string Safe(string value) => DiagnosticRedactor.Redact(value).Replace("\r", " ", StringComparison.Ordinal).Replace("\n", " ", StringComparison.Ordinal);
        var report = new StringBuilder();
        void Add(FormattableString line) => report.AppendLine(FormattableString.Invariant(line));
        report.AppendLine("# SonicRelay diagnostic report").AppendLine();
        Add($"- Generated (UTC): {DateTimeOffset.UtcNow:O}");
        Add($"- App version: {Safe(snapshot.AppVersion)}");
        Add($"- Runtime: {Safe(snapshot.RuntimeVersion)}");
        Add($"- OS: {Safe(snapshot.OsVersion)}");
        Add($"- Backend: {Safe(snapshot.Backend.Host)} ({snapshot.Backend.Status})");
        Add($"- Auth: {snapshot.Auth.Status}");
        Add($"- Device: {Safe(snapshot.Device.MaskedId)} ({snapshot.Device.Status})");
        Add($"- Session: {Safe(snapshot.Session.MaskedId)} ({snapshot.Session.Status})");
        Add($"- Signaling: {Safe(snapshot.Signaling.ConnectionState)}");
        Add($"- Viewers: {snapshot.Session.ViewerCount}");
        Add($"- Audio: {Safe(snapshot.AudioCapture.CaptureState)}");
        Add($"- Output device: {Safe(snapshot.AudioCapture.OutputDevice)}");
        Add($"- Audio level: {snapshot.AudioCapture.Level:P0}");
        Add($"- WebRTC peers: {snapshot.WebRtc.PeerCount} ({snapshot.WebRtc.Status})");
        report.AppendLine().AppendLine("## Last errors");

        if (snapshot.LastErrors.Count == 0) report.AppendLine("- None");
        else foreach (var error in snapshot.LastErrors.TakeLast(20)) Add($"- {Safe(error)}");
        return report.ToString();
    }
}
