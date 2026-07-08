using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.ApiClient.WebRtc;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Core.Audio;
using SonicRelay.Windows.Core.Configuration;
using SonicRelay.Windows.Core.Diagnostics;
using SonicRelay.Windows.Core.Storage;
using SonicRelay.Windows.Presentation;
using SonicRelay.Windows.Signaling;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.App;

public sealed class PublisherRuntime : IAsyncDisposable
{
    private readonly HttpClient httpClient;
    private readonly IPeerConnectionManager peers;
    private readonly IWebRtcPublisher webRtcPublisher;
    private readonly WebRtcAudioBridge audioBridge;
    private string? lastLoggedState;
    private bool hadActiveSession;

    private PublisherRuntime(
        HttpClient httpClient,
        PublisherWorkflow workflow,
        Uri backendBaseUrl,
        IPeerConnectionManager peers,
        IWebRtcPublisher webRtcPublisher,
        WebRtcAudioBridge audioBridge,
        RelayPreferenceStore relayPreference,
        AudioQualityStore audioQuality,
        IAudioCaptureService audioCapture,
        AudioOutputPreferenceStore audioOutput)
    {
        this.httpClient = httpClient;
        this.peers = peers;
        this.webRtcPublisher = webRtcPublisher;
        this.audioBridge = audioBridge;
        Workflow = workflow;
        BackendBaseUrl = backendBaseUrl;
        RelayPreference = relayPreference;
        AudioQuality = audioQuality;
        AudioCapture = audioCapture;
        AudioOutput = audioOutput;
        DiagnosticLog = new DiagnosticLog();
        ReportExporter = new DiagnosticReportExporter();
        Workflow.StateChanged += OnWorkflowStateChanged;
        _ = WriteDiagnosticAsync("runtime", "Publisher runtime configured.", new Dictionary<string, string>
        {
            ["backend"] = DiagnosticRedactor.BackendHost(backendBaseUrl)
        });
    }

    public PublisherWorkflow Workflow { get; }
    public Uri BackendBaseUrl { get; }
    public RelayPreferenceStore RelayPreference { get; }
    public AudioQualityStore AudioQuality { get; }
    public IAudioCaptureService AudioCapture { get; }
    public AudioOutputPreferenceStore AudioOutput { get; }
    public DiagnosticLog DiagnosticLog { get; }
    public DiagnosticReportExporter ReportExporter { get; }
    public IWebRtcPublisher WebRtcPublisher => webRtcPublisher;

    public static PublisherRuntime Create(Uri backendBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(backendBaseUrl);
        if (!backendBaseUrl.IsAbsoluteUri || backendBaseUrl.Scheme is not ("http" or "https"))
            throw new ConfigurationValidationException("Backend URL must be an absolute HTTP or HTTPS URL.");

        var normalized = backendBaseUrl.AbsoluteUri.EndsWith('/') ? backendBaseUrl : new Uri(backendBaseUrl.AbsoluteUri + "/");
        // The backend hosts the signaling WebSocket at /ws/signaling; deriving it from the backend base
        // keeps a single configured address while matching the server route (a bare /signaling returns 404).
        var signalingUrl = new Uri(normalized, "ws/signaling");
        var configuration = new PublisherConfiguration(normalized, signalingUrl, 4);
        configuration.Validate();
        var tokenStore = new UserScopedTokenStore();
        var http = new HttpClient { BaseAddress = normalized, Timeout = TimeSpan.FromSeconds(30) };

        // The WebRTC publisher needs the signaling client to send offers/candidates,
        // but the client takes its handlers up front — register the publisher through
        // a composite handler after both exist.
        var signalingHandlers = new CompositeSignalingMessageHandler();
        var signaling = new SignalingClient(configuration, tokenStore, [signalingHandlers]);
        var iceServersProvider = new BackendIceServersProvider(new WebRtcApiClient(http, tokenStore));
        var relayPreference = new RelayPreferenceStore();
        var audioQuality = new AudioQualityStore();
        var peers = new PeerConnectionManager(
            new SipSorceryPeerConnectionFactory(
                iceServersProvider,
                () => relayPreference.ForceRelay,
                () => audioQuality.CurrentProfile),
            new WebRtcPublisherOptions([new WebRtcIceServer(["stun:stun.l.google.com:19302"])]));
        var webRtcPublisher = new WebRtcPublisher(signaling, peers);
        signalingHandlers.Register(webRtcPublisher);

        var audio = new AudioCaptureService();
        var audioOutput = new AudioOutputPreferenceStore();
        // Restore the previously selected output device (null = system default).
        audio.SelectOutputDevice(audioOutput.SelectedDeviceId);
        var audioBridge = new WebRtcAudioBridge(audio, webRtcPublisher);
        var workflow = new PublisherWorkflow(
            new AuthApiClient(http, tokenStore),
            new DeviceApiClient(http, tokenStore),
            new SessionApiClient(http, tokenStore),
            signaling,
            audio,
            Environment.MachineName);
        return new PublisherRuntime(http, workflow, normalized, peers, webRtcPublisher, audioBridge, relayPreference, audioQuality, audio, audioOutput);
    }

    private void OnWorkflowStateChanged(PublisherSnapshot state)
    {
        // The publisher closes signaling locally on session end, so it never
        // receives its own session.ended; tear down peer connections here when
        // the active session clears.
        var hasSession = state.SessionId is not null;
        if (hadActiveSession && !hasSession)
        {
            _ = peers.RemoveAllAsync();
        }
        hadActiveSession = hasSession;

        var signature = $"{state.IsAuthenticated}|{state.SignalingState}|{state.AudioState}|{state.ViewerCount}|{state.ErrorMessage}";
        if (signature == lastLoggedState) return;
        lastLoggedState = signature;
        _ = WriteDiagnosticAsync("publisher-state", state.ErrorMessage ?? "Publisher status changed.", new Dictionary<string, string>
        {
            ["authenticated"] = state.IsAuthenticated.ToString(),
            ["signaling"] = state.SignalingState.ToString(),
            ["audio"] = state.AudioState.ToString(),
            ["viewerCount"] = state.ViewerCount.ToString(System.Globalization.CultureInfo.InvariantCulture)
        });
    }

    private async Task WriteDiagnosticAsync(string category, string message, IReadOnlyDictionary<string, string> properties)
    {
        try
        {
            await DiagnosticLog.WriteAsync(category, message, properties);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or ObjectDisposedException)
        {
            // Diagnostics must never interrupt publisher operation.
        }
    }

    public async ValueTask DisposeAsync()
    {
        Workflow.StateChanged -= OnWorkflowStateChanged;
        // Stop the audio pump before the workflow disposes the capture service,
        // then tear down the WebRTC publisher (which disposes the peer manager).
        await audioBridge.DisposeAsync();
        await Workflow.DisposeAsync();
        await webRtcPublisher.DisposeAsync();
        httpClient.Dispose();
        DiagnosticLog.Dispose();
    }
}
