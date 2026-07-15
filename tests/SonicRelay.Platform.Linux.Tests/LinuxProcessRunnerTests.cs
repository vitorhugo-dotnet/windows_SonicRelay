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

    [Fact]
    public async Task RunAsyncKillsProcessAndThrowsOperationCanceledWhenCallerTokenIsCancelled()
    {
        var runner = new LinuxProcessRunner();
        using var cts = new CancellationTokenSource();

        var runTask = runner.RunAsync("/bin/sleep", ["30"], TimeSpan.FromSeconds(30), cts.Token);

        await Task.Delay(TimeSpan.FromMilliseconds(200));
        cts.Cancel();

        var completed = await Task.WhenAny(runTask, Task.Delay(TimeSpan.FromSeconds(10)));
        Assert.Same(runTask, completed);
        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => runTask);
    }

    [Fact]
    public async Task ExitedNotifiesLateSubscriberForAlreadyExitedProcess()
    {
        var runner = new LinuxProcessRunner();
        await using var process = runner.Start("/bin/true", []);

        // Give the process time to actually exit before we subscribe, so we
        // exercise the "subscriber attaches after Exited already fired" race.
        await Task.Delay(TimeSpan.FromMilliseconds(200));

        var exitCodeReceived = new TaskCompletionSource<int>();
        process.Exited += code => exitCodeReceived.TrySetResult(code);

        var result = await exitCodeReceived.Task.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(0, result);
    }
}
