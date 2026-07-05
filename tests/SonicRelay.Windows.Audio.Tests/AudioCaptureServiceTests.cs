using SonicRelay.Windows.Audio;

namespace SonicRelay.Windows.Audio.Tests;

public sealed class AudioCaptureServiceTests
{
    [Fact]
    public async Task LifecycleTransitionsAreObservableAndIdempotent()
    {
        var backend = new FakeAudioCaptureBackend();
        await using var service = new AudioCaptureService(backend);
        var states = new List<AudioCaptureState>();
        service.StateChanged += states.Add;

        await service.StartAsync();
        await service.StartAsync();
        await service.PauseAsync();
        await service.PauseAsync();
        await service.ResumeAsync();
        await service.StopAsync();
        await service.StopAsync();

        Assert.Equal(1, backend.StartCount);
        Assert.Equal(1, backend.PauseCount);
        Assert.Equal(1, backend.ResumeCount);
        Assert.Equal(1, backend.StopCount);
        Assert.Equal(
            [AudioCaptureState.Starting, AudioCaptureState.Capturing, AudioCaptureState.Paused,
             AudioCaptureState.Capturing, AudioCaptureState.Stopping, AudioCaptureState.Stopped],
            states);
    }

    [Fact]
    public async Task FramesUpdateDiagnosticsAndAreForwarded()
    {
        var backend = new FakeAudioCaptureBackend();
        await using var service = new AudioCaptureService(backend);
        AudioFrame? received = null;
        service.FrameCaptured += frame => received = frame;
        await service.StartAsync();
        var frame = new AudioFrame([0, 0, 255, 127], 48_000, 1, AudioSampleFormat.Pcm16, TimeSpan.Zero);

        backend.Emit(frame, new AudioLevelSnapshot(1f, 0.707f));

        Assert.Same(frame, received);
        Assert.Equal(4, service.Diagnostics.BytesCaptured);
        Assert.Equal(1, service.Diagnostics.FramesCaptured);
        Assert.Equal(1f, service.Diagnostics.Level.Peak);
        Assert.Equal("Default speakers", service.Diagnostics.Device?.Name);
    }

    [Fact]
    public async Task StartFailureIsMappedWithoutEscapingAndCanBeStopped()
    {
        var backend = new FakeAudioCaptureBackend { StartError = new AudioCaptureException(AudioCaptureError.NoDevice, "No render device is available.") };
        await using var service = new AudioCaptureService(backend);

        await service.StartAsync();

        Assert.Equal(AudioCaptureState.Faulted, service.State);
        Assert.Equal(AudioCaptureError.NoDevice, service.Diagnostics.LastError?.Code);
        Assert.Contains("No render device", service.Diagnostics.LastError?.Message);
        await service.StopAsync();
        Assert.Equal(AudioCaptureState.Stopped, service.State);
    }

    [Theory]
    [InlineData(unchecked((int)0x80070490), AudioCaptureError.NoDevice)]
    [InlineData(unchecked((int)0x88890004), AudioCaptureError.DeviceLost)]
    [InlineData(unchecked((int)0x80070005), AudioCaptureError.AccessDenied)]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public void WasapiErrorCodesMapToStableErrors(int errorCode, AudioCaptureError expected)
    {
        Assert.Equal(expected, WasapiLoopbackBackend.MapHResult(errorCode).Error);
    }

    [Fact]
    public async Task PauseFailureFaultsServiceWithoutEscaping()
    {
        var backend = new FakeAudioCaptureBackend
        {
            PauseError = new AudioCaptureException(AudioCaptureError.DeviceLost, "The render device was disconnected.")
        };
        await using var service = new AudioCaptureService(backend);
        await service.StartAsync();

        await service.PauseAsync();

        Assert.Equal(AudioCaptureState.Faulted, service.State);
        Assert.Equal(AudioCaptureError.DeviceLost, service.Diagnostics.LastError?.Code);
    }
}

internal sealed class FakeAudioCaptureBackend : IAudioCaptureBackend
{
    public int StartCount { get; private set; }
    public int PauseCount { get; private set; }
    public int ResumeCount { get; private set; }
    public int StopCount { get; private set; }
    public AudioCaptureException? StartError { get; init; }
    public AudioCaptureException? PauseError { get; init; }
    public AudioDeviceInfo? Device { get; private set; }
    public event Action<AudioFrame, AudioLevelSnapshot>? FrameAvailable;
    public event Action<AudioCaptureException>? Faulted;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        StartCount++;
        if (StartError is not null) throw StartError;
        Device = new AudioDeviceInfo("default", "Default speakers", 48_000, 2, AudioSampleFormat.IeeeFloat32);
        return Task.CompletedTask;
    }

    public Task PauseAsync(CancellationToken cancellationToken)
    {
        PauseCount++;
        return PauseError is null ? Task.CompletedTask : Task.FromException(PauseError);
    }
    public Task ResumeAsync(CancellationToken cancellationToken) { ResumeCount++; return Task.CompletedTask; }
    public Task StopAsync(CancellationToken cancellationToken) { StopCount++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    public void Emit(AudioFrame frame, AudioLevelSnapshot level) => FrameAvailable?.Invoke(frame, level);
    public void Fail(AudioCaptureException error) => Faulted?.Invoke(error);
}
