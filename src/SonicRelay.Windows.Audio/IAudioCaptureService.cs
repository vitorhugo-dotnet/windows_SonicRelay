namespace SonicRelay.Windows.Audio;

public interface IAudioCaptureService : IAsyncDisposable
{
    AudioCaptureState State { get; }
    AudioCaptureDiagnostics Diagnostics { get; }

    /// <summary>The selected render device id, or null for the system default.</summary>
    string? PreferredDeviceId { get; }

    event Action<AudioCaptureState>? StateChanged;
    event Action<AudioFrame>? FrameCaptured;
    event Action<AudioLevelSnapshot>? LevelChanged;
    Task StartAsync(CancellationToken cancellationToken = default);
    Task StopAsync(CancellationToken cancellationToken = default);
    Task PauseAsync(CancellationToken cancellationToken = default);
    Task ResumeAsync(CancellationToken cancellationToken = default);

    /// <summary>Lists the active Windows render endpoints for the source picker.</summary>
    IReadOnlyList<AudioOutputDevice> GetOutputDevices();

    /// <summary>
    /// Selects which render endpoint to capture (null = system default). Applies to
    /// the next capture start; it does not interrupt an in-progress capture.
    /// </summary>
    void SelectOutputDevice(string? deviceId);
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
