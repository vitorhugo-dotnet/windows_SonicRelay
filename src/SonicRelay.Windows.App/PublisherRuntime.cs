using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Core.Configuration;
using SonicRelay.Windows.Core.Diagnostics;
using SonicRelay.Windows.Core.Storage;
using SonicRelay.Windows.Presentation;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.App;

public sealed class PublisherRuntime : IAsyncDisposable
{
    private readonly HttpClient httpClient;
    private string? lastLoggedState;

    private PublisherRuntime(HttpClient httpClient, PublisherWorkflow workflow, Uri backendBaseUrl)
    {
        this.httpClient = httpClient;
        Workflow = workflow;
        BackendBaseUrl = backendBaseUrl;
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
    public DiagnosticLog DiagnosticLog { get; }
    public DiagnosticReportExporter ReportExporter { get; }

    public static PublisherRuntime Create(Uri backendBaseUrl)
    {
        ArgumentNullException.ThrowIfNull(backendBaseUrl);
        if (!backendBaseUrl.IsAbsoluteUri || backendBaseUrl.Scheme is not ("http" or "https"))
            throw new ConfigurationValidationException("Backend URL must be an absolute HTTP or HTTPS URL.");

        var normalized = backendBaseUrl.AbsoluteUri.EndsWith('/') ? backendBaseUrl : new Uri(backendBaseUrl.AbsoluteUri + "/");
        var signalingUrl = new Uri(normalized, "signaling");
        var configuration = new PublisherConfiguration(normalized, signalingUrl, 4);
        configuration.Validate();
        var tokenStore = new UserScopedTokenStore();
        var http = new HttpClient { BaseAddress = normalized, Timeout = TimeSpan.FromSeconds(30) };
        var signaling = new SignalingClient(configuration, tokenStore, []);
        var workflow = new PublisherWorkflow(
            new AuthApiClient(http, tokenStore),
            new DeviceApiClient(http, tokenStore),
            new SessionApiClient(http, tokenStore),
            signaling,
            new AudioCaptureService(),
            Environment.MachineName);
        return new PublisherRuntime(http, workflow, normalized);
    }

    private void OnWorkflowStateChanged(PublisherSnapshot state)
    {
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
        await Workflow.DisposeAsync();
        httpClient.Dispose();
        DiagnosticLog.Dispose();
    }
}
