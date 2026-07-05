using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Core.Storage;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.Presentation.Tests;

public sealed class PublisherWorkflowTests
{
    [Theory]
    [InlineData("", "password", "Email is required.")]
    [InlineData("user@example.com", "", "Password is required.")]
    public async Task LoginRejectsRequiredFields(string email, string password, string expected)
    {
        await using var fixture = new Fixture();
        await fixture.Workflow.LoginAsync(email, password);
        Assert.Equal(expected, fixture.Workflow.State.ErrorMessage);
        Assert.False(fixture.Auth.LoginCalled);
    }

    [Fact]
    public async Task LoginReusesActiveWindowsPublisherDevice()
    {
        await using var fixture = new Fixture();
        var device = Device("publisher", revoked: false);
        fixture.Devices.Items.Add(device);

        await fixture.Workflow.LoginAsync("user@example.com", "password");

        Assert.True(fixture.Workflow.State.IsAuthenticated);
        Assert.Equal(device.Id, fixture.Workflow.State.DeviceId);
        Assert.False(fixture.Devices.RegisterCalled);
    }

    [Fact]
    public async Task CreateSessionConnectsSignalingAndExposesCode()
    {
        await using var fixture = new Fixture();
        await fixture.Workflow.LoginAsync("user@example.com", "password");

        await fixture.Workflow.CreateSessionAsync();

        Assert.Equal("ABC123", fixture.Workflow.State.SessionCode);
        Assert.Equal(SignalingConnectionState.Connected, fixture.Workflow.State.SignalingState);
        Assert.Equal(fixture.Sessions.Created.Id.ToString("D"), fixture.Signaling.SessionId);
        Assert.True(fixture.Workflow.State.CanStartAudio);
    }

    [Fact]
    public async Task CommandsAreGatedByPrerequisites()
    {
        await using var fixture = new Fixture();
        await fixture.Workflow.CreateSessionAsync();
        Assert.Equal("Sign in and register this device before creating a session.", fixture.Workflow.State.ErrorMessage);

        await fixture.Workflow.StartAudioAsync();
        Assert.Equal("Create a session and connect signaling before starting audio.", fixture.Workflow.State.ErrorMessage);
        Assert.False(fixture.Audio.StartCalled);
    }

    [Fact]
    public async Task EndSessionStopsAudioClosesSignalingAndCallsBackend()
    {
        await using var fixture = new Fixture();
        await fixture.Workflow.LoginAsync("user@example.com", "password");
        await fixture.Workflow.CreateSessionAsync();
        await fixture.Workflow.StartAudioAsync();

        await fixture.Workflow.EndSessionAsync();

        Assert.True(fixture.Audio.StopCalled);
        Assert.True(fixture.Signaling.CloseCalled);
        Assert.Equal(fixture.Sessions.Created.Id, fixture.Sessions.EndedId);
        Assert.Null(fixture.Workflow.State.SessionId);
    }

    [Fact]
    public async Task FailureIsVisibleAndNotReportedAsSuccess()
    {
        await using var fixture = new Fixture();
        fixture.Auth.Exception = new HttpRequestException("network detail");

        await fixture.Workflow.LoginAsync("user@example.com", "password");

        Assert.False(fixture.Workflow.State.IsAuthenticated);
        Assert.Contains("network detail", fixture.Workflow.State.ErrorMessage);
    }

    private static DeviceResponse Device(string name, bool revoked) =>
        new(Guid.NewGuid(), name, "windows_publisher", "windows", null, true, revoked, null, DateTimeOffset.UtcNow);

    private sealed class Fixture : IAsyncDisposable
    {
        public FakeAuth Auth { get; } = new();
        public FakeDevices Devices { get; } = new();
        public FakeSessions Sessions { get; } = new();
        public FakeSignaling Signaling { get; } = new();
        public FakeAudio Audio { get; } = new();
        public PublisherWorkflow Workflow { get; }

        public Fixture()
        {
            Workflow = new PublisherWorkflow(Auth, Devices, Sessions, Signaling, Audio, "Test PC");
        }

        public ValueTask DisposeAsync() => Workflow.DisposeAsync();
    }

    private sealed class FakeAuth : IAuthApiClient
    {
        public bool LoginCalled { get; private set; }
        public Exception? Exception { get; set; }
        public Task<TokenSet> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
        {
            LoginCalled = true;
            return Exception is null
                ? Task.FromResult(new TokenSet("access", "refresh", DateTimeOffset.UtcNow.AddHours(1)))
                : Task.FromException<TokenSet>(Exception);
        }
        public Task<TokenSet> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new CurrentUserResponse(Guid.NewGuid(), "user@example.com", "User", true, DateTimeOffset.UtcNow, null));
    }

    private sealed class FakeDevices : IDeviceApiClient
    {
        public List<DeviceResponse> Items { get; } = [];
        public bool RegisterCalled { get; private set; }
        public Task<IReadOnlyList<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<DeviceResponse>>(Items);
        public Task<DeviceResponse> RegisterWindowsPublisherAsync(RegisterDeviceRequest request, CancellationToken cancellationToken = default)
        {
            RegisterCalled = true;
            return Task.FromResult(Device(request.Name, revoked: false));
        }
    }

    private sealed class FakeSessions : ISessionApiClient
    {
        public StreamSessionResponse Created { get; } = new(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "active", 4, DateTimeOffset.UtcNow.AddMinutes(5), DateTimeOffset.UtcNow, null, DateTimeOffset.UtcNow, "ABC123");
        public Guid? EndedId { get; private set; }
        public Task<StreamSessionResponse> CreateSessionAsync(CreateSessionRequest request, CancellationToken cancellationToken = default) => Task.FromResult(Created with { SourceDeviceId = request.SourceDeviceId });
        public Task<IReadOnlyList<ActiveSessionResponse>> GetActiveSessionsAsync(CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ActiveSessionResponse>>([]);
        public Task<StreamSessionResponse> EndSessionAsync(Guid sessionId, CancellationToken cancellationToken = default)
        {
            EndedId = sessionId;
            return Task.FromResult(Created with { Id = sessionId, Status = "ended", EndedAt = DateTimeOffset.UtcNow });
        }
    }

    private sealed class FakeSignaling : ISignalingClient
    {
        public SignalingConnectionState State { get; private set; } = SignalingConnectionState.Disconnected;
        public string? SessionId { get; private set; }
        public bool CloseCalled { get; private set; }
        public event Action<SignalingConnectionState>? StateChanged;
        public Task ConnectAsync(string sessionId, string deviceId, CancellationToken cancellationToken = default)
        {
            SessionId = sessionId;
            State = SignalingConnectionState.Connected;
            StateChanged?.Invoke(State);
            return Task.CompletedTask;
        }
        public Task SendAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default)
        {
            CloseCalled = true;
            State = SignalingConnectionState.Closed;
            StateChanged?.Invoke(State);
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakeAudio : IAudioCaptureService
    {
        public AudioCaptureState State { get; private set; } = AudioCaptureState.Stopped;
        public AudioCaptureDiagnostics Diagnostics => new(State, null, null, AudioLevelSnapshot.Silence, 0, 0);
        public bool StartCalled { get; private set; }
        public bool StopCalled { get; private set; }
        public event Action<AudioCaptureState>? StateChanged;
        public event Action<AudioFrame>? FrameCaptured { add { } remove { } }
        public event Action<AudioLevelSnapshot>? LevelChanged { add { } remove { } }
        public Task StartAsync(CancellationToken cancellationToken = default) { StartCalled = true; State = AudioCaptureState.Capturing; StateChanged?.Invoke(State); return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken = default) { StopCalled = true; State = AudioCaptureState.Stopped; StateChanged?.Invoke(State); return Task.CompletedTask; }
        public Task PauseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task ResumeAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
