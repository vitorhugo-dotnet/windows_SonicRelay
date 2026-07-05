using SonicRelay.Windows.Core.Diagnostics;

namespace SonicRelay.Windows.Core.Tests;

public sealed class DiagnosticReportTests
{
    [Fact]
    public async Task ExportAsyncWritesSanitizedReportInsideRequestedDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sonicrelay-{Guid.NewGuid():N}");
        try
        {
            var snapshot = CreateSnapshot(["password=hunter2", "candidate:1 1 UDP 1 10.0.0.1 5000 typ host"]);

            var path = await new DiagnosticReportExporter(directory).ExportAsync(snapshot);
            var report = await File.ReadAllTextAsync(path);

            Assert.Equal(directory, Path.GetDirectoryName(path));
            Assert.Contains("# SonicRelay diagnostic report", report, StringComparison.Ordinal);
            Assert.DoesNotContain("hunter2", report, StringComparison.Ordinal);
            Assert.DoesNotContain("10.0.0.1", report, StringComparison.Ordinal);
            Assert.Contains("[REDACTED]", report, StringComparison.Ordinal);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    [Fact]
    public async Task DiagnosticLogWritesOnlyRedactedJsonLines()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"sonicrelay-{Guid.NewGuid():N}");
        try
        {
            var log = new DiagnosticLog(directory);

            await log.WriteAsync("auth", "Login failed password=hunter2", new Dictionary<string, string> { ["token"] = "secret" });
            var content = await File.ReadAllTextAsync(log.LogPath);

            Assert.DoesNotContain("hunter2", content, StringComparison.Ordinal);
            Assert.DoesNotContain("secret", content, StringComparison.Ordinal);
            Assert.Single(log.RecentEvents);
        }
        finally
        {
            if (Directory.Exists(directory)) Directory.Delete(directory, true);
        }
    }

    private static DiagnosticsSnapshot CreateSnapshot(IReadOnlyList<string> errors) => new(
        "1.0.0", ".NET 10", "Windows", new(DiagnosticStatus.Healthy, "https://relay.test"),
        new(DiagnosticStatus.Healthy, true), new(DiagnosticStatus.Healthy, "1234…cdef"),
        new(DiagnosticStatus.Healthy, "abcd…1234", 2), new(DiagnosticStatus.Healthy, "Connected"),
        new(DiagnosticStatus.Healthy, "Capturing", "Speakers", 0.5), new(DiagnosticStatus.Healthy, 2), errors);
}
