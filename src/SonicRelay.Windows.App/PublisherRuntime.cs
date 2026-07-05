using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Core.Configuration;
using SonicRelay.Windows.Core.Storage;
using SonicRelay.Windows.Presentation;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.App;

public sealed class PublisherRuntime : IAsyncDisposable
{
    private readonly HttpClient httpClient;

    private PublisherRuntime(HttpClient httpClient, PublisherWorkflow workflow)
    {
        this.httpClient = httpClient;
        Workflow = workflow;
    }

    public PublisherWorkflow Workflow { get; }

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
        return new PublisherRuntime(http, workflow);
    }

    public async ValueTask DisposeAsync()
    {
        await Workflow.DisposeAsync();
        httpClient.Dispose();
    }
}
