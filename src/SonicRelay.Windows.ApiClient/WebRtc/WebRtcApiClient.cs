using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.WebRtc;

public interface IWebRtcApiClient
{
    Task<IceServersResponse> GetIceServersAsync(CancellationToken cancellationToken = default);
}

public sealed record IceServersResponse(IReadOnlyList<IceServerResponse> IceServers, int TtlSeconds);

public sealed record IceServerResponse(IReadOnlyList<string> Urls, string? Username = null, string? Credential = null);

public sealed class WebRtcApiClient(HttpClient httpClient, ITokenStore tokenStore) : IWebRtcApiClient
{
    private readonly ApiHttpClient _api = new(httpClient, tokenStore);

    public Task<IceServersResponse> GetIceServersAsync(CancellationToken cancellationToken = default) =>
        _api.SendAsync<IceServersResponse>(HttpMethod.Get, "/api/webrtc/ice-servers", null, true, cancellationToken);
}
