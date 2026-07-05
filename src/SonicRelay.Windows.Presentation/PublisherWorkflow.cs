using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Errors;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.Presentation;

public sealed class PublisherWorkflow : IAsyncDisposable
{
    private readonly IAuthApiClient auth;
    private readonly IDeviceApiClient devices;
    private readonly ISessionApiClient sessions;
    private readonly ISignalingClient signaling;
    private readonly IAudioCaptureService audio;
    private readonly string deviceName;
    private readonly SemaphoreSlim operationLock = new(1, 1);
    private bool disposed;

    public PublisherWorkflow(
        IAuthApiClient auth,
        IDeviceApiClient devices,
        ISessionApiClient sessions,
        ISignalingClient signaling,
        IAudioCaptureService audio,
        string deviceName)
    {
        this.auth = auth ?? throw new ArgumentNullException(nameof(auth));
        this.devices = devices ?? throw new ArgumentNullException(nameof(devices));
        this.sessions = sessions ?? throw new ArgumentNullException(nameof(sessions));
        this.signaling = signaling ?? throw new ArgumentNullException(nameof(signaling));
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        this.deviceName = string.IsNullOrWhiteSpace(deviceName) ? "Windows Publisher" : deviceName.Trim();
        signaling.StateChanged += OnSignalingStateChanged;
        audio.StateChanged += OnAudioStateChanged;
        audio.LevelChanged += OnAudioLevelChanged;
        State = new PublisherSnapshot { AudioDiagnostics = audio.Diagnostics };
    }

    public PublisherSnapshot State { get; private set; }
    public event Action<PublisherSnapshot>? StateChanged;

    public Task LoginAsync(string email, string password, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email)) return SetValidationErrorAsync("Email is required.");
        if (string.IsNullOrWhiteSpace(password)) return SetValidationErrorAsync("Password is required.");
        return ExecuteAsync(async token =>
        {
            await auth.LoginAsync(new LoginRequest(email.Trim(), password), token);
            var user = await auth.GetCurrentUserAsync(token);
            var available = await devices.GetDevicesAsync(token);
            var device = available.FirstOrDefault(item =>
                item.Type == "windows_publisher" && item.Platform == "windows" && !item.Revoked)
                ?? await devices.RegisterWindowsPublisherAsync(new RegisterDeviceRequest(deviceName, null), token);
            SetState(State with
            {
                IsAuthenticated = true,
                UserDisplayName = user.DisplayName ?? user.Email,
                DeviceId = device.Id,
                DeviceName = device.Name
            }, "Signed in and publisher device is ready.");
        }, cancellationToken);
    }

    public Task CreateSessionAsync(CancellationToken cancellationToken = default)
    {
        if (!State.IsAuthenticated || State.DeviceId is null)
            return SetValidationErrorAsync("Sign in and register this device before creating a session.");
        if (State.SessionId is not null) return SetValidationErrorAsync("A publisher session is already active.");
        return ExecuteAsync(async token =>
        {
            var session = await sessions.CreateSessionAsync(new CreateSessionRequest(State.DeviceId.Value), token);
            SetState(State with { SessionId = session.Id, SessionCode = session.Code, ViewerCount = 0 }, "Session created.");
            try
            {
                await signaling.ConnectAsync(session.Id.ToString("D"), State.DeviceId.Value.ToString("D"), token);
                await RefreshViewerCountCoreAsync(token);
            }
            catch
            {
                try { await sessions.EndSessionAsync(session.Id, CancellationToken.None); } catch { }
                SetState(State with { SessionId = null, SessionCode = null, ViewerCount = 0 });
                throw;
            }
        }, cancellationToken);
    }

    public Task RefreshViewerCountAsync(CancellationToken cancellationToken = default) =>
        State.SessionId is null ? Task.CompletedTask : ExecuteAsync(RefreshViewerCountCoreAsync, cancellationToken);

    public Task StartAudioAsync(CancellationToken cancellationToken = default)
    {
        if (State.SessionId is null || State.SignalingState != SignalingConnectionState.Connected)
            return SetValidationErrorAsync("Create a session and connect signaling before starting audio.");
        return ExecuteAsync(async token => { await audio.StartAsync(token); AddLog("Audio capture started."); }, cancellationToken);
    }

    public Task StopAudioAsync(CancellationToken cancellationToken = default) =>
        ExecuteAsync(async token => { await audio.StopAsync(token); AddLog("Audio capture stopped."); }, cancellationToken);

    public Task EndSessionAsync(CancellationToken cancellationToken = default)
    {
        if (State.SessionId is null) return SetValidationErrorAsync("There is no active session to end.");
        return ExecuteAsync(async token =>
        {
            var sessionId = State.SessionId.Value;
            if (audio.State is not AudioCaptureState.Stopped) await audio.StopAsync(token);
            await signaling.CloseAsync(token);
            await sessions.EndSessionAsync(sessionId, token);
            SetState(State with { SessionId = null, SessionCode = null, ViewerCount = 0 }, "Session ended.");
        }, cancellationToken);
    }

    private async Task RefreshViewerCountCoreAsync(CancellationToken cancellationToken)
    {
        if (State.SessionId is not { } id) return;
        var active = await sessions.GetActiveSessionsAsync(cancellationToken);
        var current = active.FirstOrDefault(item => item.Id == id);
        SetState(State with { ViewerCount = current?.ViewerCount ?? 0 });
    }

    private async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken cancellationToken)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        await operationLock.WaitAsync(cancellationToken);
        try
        {
            SetState(State with { IsBusy = true, ErrorMessage = null });
            await operation(cancellationToken);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            SetState(State with { ErrorMessage = "The operation was cancelled." });
        }
        catch (Exception exception)
        {
            SetState(State with { ErrorMessage = ToFriendlyMessage(exception) }, $"Error: {ToFriendlyMessage(exception)}");
        }
        finally
        {
            SetState(State with { IsBusy = false });
            operationLock.Release();
        }
    }

    private Task SetValidationErrorAsync(string message)
    {
        SetState(State with { ErrorMessage = message }, $"Validation: {message}");
        return Task.CompletedTask;
    }

    private static string ToFriendlyMessage(Exception exception) => exception switch
    {
        ApiClientException api => api.Kind switch
        {
            ApiErrorKind.Unauthorized => "Login failed. Check your email and password.",
            ApiErrorKind.NetworkUnavailable => "The backend network is unavailable. Check the URL and connection.",
            ApiErrorKind.BackendUnavailable => "The backend is unavailable. Try again shortly.",
            _ => api.Message
        },
        AudioCaptureException audioException => audioException.Message,
        _ => exception.Message
    };

    private void OnSignalingStateChanged(SignalingConnectionState state) => SetState(State with { SignalingState = state }, $"Signaling: {state}.");
    private void OnAudioStateChanged(AudioCaptureState state) => SetState(State with { AudioState = state, AudioDiagnostics = audio.Diagnostics });
    private void OnAudioLevelChanged(AudioLevelSnapshot _) => SetState(State with { AudioDiagnostics = audio.Diagnostics });

    private void AddLog(string message) => SetState(State, message);

    private void SetState(PublisherSnapshot next, string? logMessage = null)
    {
        if (logMessage is not null)
        {
            var logs = next.ActivityLog.Append($"{DateTimeOffset.Now:HH:mm:ss} {logMessage}").TakeLast(100).ToArray();
            next = next with { ActivityLog = logs };
        }
        State = next;
        StateChanged?.Invoke(next);
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        signaling.StateChanged -= OnSignalingStateChanged;
        audio.StateChanged -= OnAudioStateChanged;
        audio.LevelChanged -= OnAudioLevelChanged;
        if (audio.State is not AudioCaptureState.Stopped)
        {
            try { await audio.StopAsync(); } catch { }
        }
        try { await signaling.CloseAsync(); } catch { }
        await audio.DisposeAsync();
        await signaling.DisposeAsync();
        operationLock.Dispose();
    }
}
