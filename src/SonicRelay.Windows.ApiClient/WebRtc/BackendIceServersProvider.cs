using SonicRelay.Windows.ApiClient.Errors;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.ApiClient.WebRtc;

/// <summary>
/// Supplies ICE servers to the WebRTC layer from the backend
/// <c>/api/webrtc/ice-servers</c> endpoint, caching them until shortly before
/// the returned TURN credentials expire. Never throws: on failure it returns
/// the last good result, or a public-STUN fallback, so peer creation always
/// proceeds.
/// </summary>
public sealed class BackendIceServersProvider(IWebRtcApiClient apiClient, TimeProvider? timeProvider = null) : IIceServersProvider
{
    private static readonly IReadOnlyList<WebRtcIceServer> StunFallback =
        [new WebRtcIceServer(["stun:stun.l.google.com:19302"])];

    private readonly IWebRtcApiClient apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
    private readonly TimeProvider timeProvider = timeProvider ?? TimeProvider.System;
    private readonly SemaphoreSlim gate = new(1, 1);
    private IReadOnlyList<WebRtcIceServer>? cached;
    private DateTimeOffset cacheExpiresAt = DateTimeOffset.MinValue;

    public async Task<IReadOnlyList<WebRtcIceServer>> GetIceServersAsync(CancellationToken cancellationToken = default)
    {
        var now = timeProvider.GetUtcNow();
        if (cached is not null && now < cacheExpiresAt) return cached;

        await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            now = timeProvider.GetUtcNow();
            if (cached is not null && now < cacheExpiresAt) return cached;

            var response = await apiClient.GetIceServersAsync(cancellationToken).ConfigureAwait(false);
            cached = response.IceServers
                .Where(server => server.Urls is { Count: > 0 })
                .Select(server => new WebRtcIceServer(server.Urls, server.Username, server.Credential))
                .ToArray();
            // Refresh a minute before the credentials lapse so a renegotiation
            // never starts with a stale TURN username.
            var ttl = Math.Max(response.TtlSeconds - 60, 30);
            cacheExpiresAt = now.AddSeconds(ttl);
            return cached.Count > 0 ? cached : StunFallback;
        }
        catch (Exception exception) when (exception is not OperationCanceledException and (ApiClientException or HttpRequestException))
        {
            return cached ?? StunFallback;
        }
        finally
        {
            gate.Release();
        }
    }
}
