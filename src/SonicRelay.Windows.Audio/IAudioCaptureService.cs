namespace SonicRelay.Windows.Audio;

public interface IAudioCaptureService : IAsyncDisposable
{
    AudioCaptureState State { get; }
    AudioCaptureDiagnostics Diagnostics { get; }
    event Action<AudioCaptureState>? StateChanged;
    event Action<AudioFrame>? FrameCaptured;
    event Action<AudioLevelSnapshot>? LevelChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);
}

internal interface IAudioCaptureBackend : IAsyncDisposable
{
    AudioDeviceInfo? Device { get; }
    event Action<AudioFrame, AudioLevelSnapshot>? FrameAvailable;
    event Action<AudioCaptureException>? Faulted;
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
    Task PauseAsync(CancellationToken cancellationToken);
    Task ResumeAsync(CancellationToken cancellationToken);
}
