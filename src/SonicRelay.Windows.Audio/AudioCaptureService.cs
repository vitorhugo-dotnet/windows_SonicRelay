namespace SonicRelay.Windows.Audio;

public sealed class AudioCaptureService : IAudioCaptureService
{
    private readonly IAudioCaptureBackend _backend;
    private readonly SemaphoreSlim _lifecycle = new(1, 1);
    private AudioCaptureDiagnostics _diagnostics = new(AudioCaptureState.Stopped, null, null, AudioLevelSnapshot.Silence, 0, 0);
    private bool _disposed;

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public AudioCaptureService() : this(new WasapiLoopbackBackend()) { }

    internal AudioCaptureService(IAudioCaptureBackend backend)
    {
        _backend = backend;
        _backend.FrameAvailable += OnFrameAvailable;
        _backend.Faulted += OnBackendFaulted;
    }

    public AudioCaptureState State => _diagnostics.State;
    public AudioCaptureDiagnostics Diagnostics => _diagnostics;
    public event Action<AudioCaptureState>? StateChanged;
    public event Action<AudioFrame>? FrameCaptured;
    public event Action<AudioLevelSnapshot>? LevelChanged;

    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State is AudioCaptureState.Capturing or AudioCaptureState.Paused or AudioCaptureState.Starting) return;
            SetState(AudioCaptureState.Starting);
            _diagnostics = _diagnostics with { LastError = null, Level = AudioLevelSnapshot.Silence, BytesCaptured = 0, FramesCaptured = 0 };
            try
            {
                await _backend.StartAsync(cancellationToken).ConfigureAwait(false);
                _diagnostics = _diagnostics with { Device = _backend.Device };
                SetState(AudioCaptureState.Capturing);
            }
            catch (AudioCaptureException error)
            {
                SetFailure(error);
            }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                SetFailure(new AudioCaptureException(AudioCaptureError.PlatformFailure, "Windows audio capture could not be started.", error));
            }
        }
        finally { _lifecycle.Release(); }
    }

    public async Task StopAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (State is AudioCaptureState.Stopped or AudioCaptureState.Stopping) return;
            SetState(AudioCaptureState.Stopping);
            try { await _backend.StopAsync(cancellationToken).ConfigureAwait(false); }
            catch (Exception error) when (error is not OperationCanceledException)
            {
                _diagnostics = _diagnostics with { LastError = new(AudioCaptureError.PlatformFailure, error.Message) };
            }
            _diagnostics = _diagnostics with { Device = null, Level = AudioLevelSnapshot.Silence };
            SetState(AudioCaptureState.Stopped);
        }
        finally { _lifecycle.Release(); }
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State == AudioCaptureState.Paused) return;
            if (State != AudioCaptureState.Capturing) return;
            try
            {
                await _backend.PauseAsync(cancellationToken).ConfigureAwait(false);
                SetState(AudioCaptureState.Paused);
            }
            catch (AudioCaptureException error) { SetFailure(error); }
        }
        finally { _lifecycle.Release(); }
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await _lifecycle.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            if (State == AudioCaptureState.Capturing) return;
            if (State != AudioCaptureState.Paused) return;
            try
            {
                await _backend.ResumeAsync(cancellationToken).ConfigureAwait(false);
                SetState(AudioCaptureState.Capturing);
            }
            catch (AudioCaptureException error) { SetFailure(error); }
        }
        finally { _lifecycle.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed) return;
        await StopAsync().ConfigureAwait(false);
        _disposed = true;
        _backend.FrameAvailable -= OnFrameAvailable;
        _backend.Faulted -= OnBackendFaulted;
        await _backend.DisposeAsync().ConfigureAwait(false);
        _lifecycle.Dispose();
    }

    private void OnFrameAvailable(AudioFrame frame, AudioLevelSnapshot level)
    {
        if (State != AudioCaptureState.Capturing) return;
        _diagnostics = _diagnostics with
        {
            Level = level,
            BytesCaptured = _diagnostics.BytesCaptured + frame.Data.Length,
            FramesCaptured = _diagnostics.FramesCaptured + 1
        };
        FrameCaptured?.Invoke(frame);
        LevelChanged?.Invoke(level);
    }

    private void OnBackendFaulted(AudioCaptureException error) => SetFailure(error);

    private void SetFailure(AudioCaptureException error)
    {
        _diagnostics = _diagnostics with { LastError = new(error.Error, error.Message) };
        SetState(AudioCaptureState.Faulted);
    }

    private void SetState(AudioCaptureState state)
    {
        if (State == state) return;
        _diagnostics = _diagnostics with { State = state };
        StateChanged?.Invoke(state);
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(_disposed, this);
}
