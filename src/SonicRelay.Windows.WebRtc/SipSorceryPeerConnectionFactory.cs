using SIPSorcery.Net;

namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Creates <see cref="SipSorceryPeerConnection"/> instances, resolving fresh
/// ICE servers (including short-lived TURN credentials) from the injected
/// provider for every viewer so relayed sessions never start with expired
/// credentials. Falls back to the statically configured options if the
/// provider yields nothing.
/// </summary>
public sealed class SipSorceryPeerConnectionFactory(IIceServersProvider iceServersProvider) : IWebRtcPeerConnectionFactory
{
    private readonly IIceServersProvider iceServersProvider =
        iceServersProvider ?? throw new ArgumentNullException(nameof(iceServersProvider));

    public async Task<IWebRtcPeerConnection> CreateAsync(
        string viewerId,
        WebRtcPublisherOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ArgumentNullException.ThrowIfNull(options);

        var servers = await ResolveIceServersAsync(options, cancellationToken).ConfigureAwait(false);
        var configuration = new RTCConfiguration { iceServers = MapIceServers(servers) };
        return new SipSorceryPeerConnection(viewerId, new RTCPeerConnection(configuration));
    }

    private async Task<IReadOnlyList<WebRtcIceServer>> ResolveIceServersAsync(
        WebRtcPublisherOptions options,
        CancellationToken cancellationToken)
    {
        try
        {
            var resolved = await iceServersProvider.GetIceServersAsync(cancellationToken).ConfigureAwait(false);
            if (resolved is { Count: > 0 }) return resolved;
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            // The provider is expected to be resilient, but never let ICE-server
            // resolution abort peer creation; fall back to configured defaults.
        }
        return options.IceServers;
    }

    // SIPSorcery's RTCIceServer.urls holds a single URL, so each URL becomes its
    // own entry while sharing the TURN credentials.
    private static List<RTCIceServer> MapIceServers(IReadOnlyList<WebRtcIceServer> servers)
    {
        var mapped = new List<RTCIceServer>();
        foreach (var server in servers)
        {
            foreach (var url in server.Urls)
            {
                mapped.Add(new RTCIceServer
                {
                    urls = url,
                    username = server.Username,
                    credential = server.Credential,
                    credentialType = RTCIceCredentialType.password
                });
            }
        }
        return mapped;
    }
}
