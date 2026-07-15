using SonicRelay.Platform.Linux.Audio;

namespace SonicRelay.Platform.Linux.Tests;

public sealed class LinuxProcessRunnerTests
{
    [Fact]
    public async Task RunAsyncCapturesStdoutAndExitCodeForARealProcess()
    {
        var runner = new LinuxProcessRunner();
        var result = await runner.RunAsync("/bin/echo", ["hello"], TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(0, result.ExitCode);
        Assert.Contains("hello", result.StandardOutput);
    }

    [Fact]
    public async Task RunAsyncReportsNonZeroExitCode()
    {
        var runner = new LinuxProcessRunner();
        var result = await runner.RunAsync("/bin/sh", ["-c", "exit 3"], TimeSpan.FromSeconds(5), CancellationToken.None);

        Assert.Equal(3, result.ExitCode);
    }
}
