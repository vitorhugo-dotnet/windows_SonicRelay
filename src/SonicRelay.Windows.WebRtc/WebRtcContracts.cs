using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.WebRtc;

public enum PeerConnectionState
{
    New,
    Connecting,
    Connected,
    Disconnected,
    Failed,
    Closed
}

public sealed record WebRtcSessionDescription(string Type, string Sdp);

public sealed record WebRtcIceCandidate(string Candidate, string? SdpMid = null, int? SdpMLineIndex = null);

public sealed class WebRtcAudioFrame
{
    private readonly byte[] data;

    public WebRtcAudioFrame(ReadOnlySpan<byte> data, int sampleRate, int channelCount, TimeSpan timestamp)
    {
        if (data.IsEmpty) throw new ArgumentException("Audio frame data cannot be empty.", nameof(data));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        if (channelCount is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(channelCount));
        ArgumentOutOfRangeException.ThrowIfLessThan(timestamp, TimeSpan.Zero);
        this.data = data.ToArray();
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Timestamp = timestamp;
    }

    public ReadOnlyMemory<byte> Data => data;
    public int SampleRate { get; }
    public int ChannelCount { get; }
    public TimeSpan Timestamp { get; }
}

public sealed class WebRtcPublisherOptions
{
    public WebRtcPublisherOptions(IEnumerable<string>? iceServers = null)
    {
        IceServers = (iceServers ?? []).Select(server =>
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(server);
            return server;
        }).ToArray();
    }

    public IReadOnlyList<string> IceServers { get; }
}

public sealed record PeerConnectionDiagnostics(
    string ViewerId,
    PeerConnectionState State,
    string? SelectedCandidatePair = null,
    TimeSpan? EstimatedRoundTripTime = null);

public sealed record WebRtcPublisherDiagnostics(
    int ViewerConnectionCount,
    IReadOnlyList<PeerConnectionDiagnostics> Viewers,
    string? LastError = null);

public sealed record ViewerPeer(string ViewerId, IWebRtcPeerConnection Connection);

public sealed record ViewerPeerRegistration(ViewerPeer Peer, bool WasCreated);

public interface IWebRtcPeerConnection : IAsyncDisposable
{
    string ViewerId { get; }
    PeerConnectionDiagnostics Diagnostics { get; }
    event Func<WebRtcIceCandidate, CancellationToken, Task>? LocalIceCandidateReady;
    event Action? DiagnosticsChanged;
    Task<WebRtcSessionDescription> CreateOfferAsync(CancellationToken cancellationToken = default);
    Task ApplyAnswerAsync(WebRtcSessionDescription answer, CancellationToken cancellationToken = default);
    Task AddRemoteIceCandidateAsync(WebRtcIceCandidate candidate, CancellationToken cancellationToken = default);
    Task SendAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default);
}

public interface IWebRtcPeerConnectionFactory
{
    Task<IWebRtcPeerConnection> CreateAsync(
        string viewerId,
        WebRtcPublisherOptions options,
        CancellationToken cancellationToken = default);
}

public interface IPeerConnectionManager : IAsyncDisposable
{
    int ViewerCount { get; }
    event Func<string, WebRtcIceCandidate, CancellationToken, Task>? LocalIceCandidateReady;
    event Action? DiagnosticsChanged;
    Task<ViewerPeerRegistration> RegisterViewerAsync(string viewerId, CancellationToken cancellationToken = default);
    Task ApplyAnswerAsync(string viewerId, WebRtcSessionDescription answer, CancellationToken cancellationToken = default);
    Task AddRemoteIceCandidateAsync(string viewerId, WebRtcIceCandidate candidate, CancellationToken cancellationToken = default);
    Task PushAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default);
    Task<bool> RemoveViewerAsync(string viewerId, CancellationToken cancellationToken = default);
    Task RemoveAllAsync(CancellationToken cancellationToken = default);
    IReadOnlyList<PeerConnectionDiagnostics> GetDiagnostics();
}

public interface IWebRtcPublisher : ISignalingMessageHandler, IAsyncDisposable
{
    WebRtcPublisherDiagnostics Diagnostics { get; }
    event Action<WebRtcPublisherDiagnostics>? DiagnosticsChanged;
    Task PushAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default);
}

public sealed class WebRtcPublisherException(string message, Exception? innerException = null)
    : Exception(message, innerException);
