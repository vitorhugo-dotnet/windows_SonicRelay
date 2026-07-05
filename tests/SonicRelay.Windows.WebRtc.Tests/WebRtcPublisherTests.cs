using System.Text.Json;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class WebRtcPublisherTests
{
    [Fact]
    public async Task ViewerReadyRegistersPeerAndSendsOfferOnlyOnce()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;

        var ready = new SignalingMessageEnvelope(SignalingMessageTypes.ViewerReady, "session-1", "viewer-1");
        await publisher.HandleAsync(ready);
        await publisher.HandleAsync(ready);

        var peer = Assert.Single(context.Factory.Peers);
        Assert.Equal("viewer-1", peer.ViewerId);
        Assert.Equal(1, peer.OfferCount);
        var offer = Assert.Single(context.Signaling.Messages);
        Assert.Equal(SignalingMessageTypes.WebRtcOffer, offer.Type);
        Assert.Equal("session-1", offer.SessionId);
        Assert.Equal("viewer-1", offer.ViewerId);
        Assert.Equal("offer-viewer-1", offer.Payload!.Value.GetProperty("sdp").GetString());
        Assert.Equal(1, publisher.Diagnostics.ViewerConnectionCount);
    }

    [Fact]
    public async Task AnswerAndRemoteCandidateRouteToAddressedViewer()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;
        await ReadyAsync(publisher, "viewer-1");
        await ReadyAsync(publisher, "viewer-2");
        var answer = JsonSerializer.SerializeToElement(new WebRtcSessionDescription("answer", "answer-sdp"));
        var candidate = JsonSerializer.SerializeToElement(new WebRtcIceCandidate("candidate-2", "audio", 0));

        await publisher.HandleAsync(new(SignalingMessageTypes.WebRtcAnswer, "session-1", "viewer-2", answer));
        await publisher.HandleAsync(new(SignalingMessageTypes.WebRtcIceCandidate, "session-1", "viewer-2", candidate));

        var first = context.Factory.Peers.Single(peer => peer.ViewerId == "viewer-1");
        var second = context.Factory.Peers.Single(peer => peer.ViewerId == "viewer-2");
        Assert.Null(first.Answer);
        Assert.Empty(first.RemoteCandidates);
        Assert.Equal("answer-sdp", second.Answer?.Sdp);
        Assert.Equal("candidate-2", Assert.Single(second.RemoteCandidates).Candidate);
    }

    [Fact]
    public async Task LocalCandidateIsEmittedThroughSignalingForOwningViewer()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;
        await ReadyAsync(publisher, "viewer-1");
        context.Signaling.Messages.Clear();

        await context.Factory.Peers[0].EmitCandidateAsync(new("local-candidate", "audio", 0));

        var message = Assert.Single(context.Signaling.Messages);
        Assert.Equal(SignalingMessageTypes.WebRtcIceCandidate, message.Type);
        Assert.Equal("viewer-1", message.ViewerId);
        Assert.Equal("local-candidate", message.Payload!.Value.GetProperty("candidate").GetString());
    }

    [Fact]
    public async Task AudioIsFannedOutAndDiagnosticsTrackPeerState()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;
        await ReadyAsync(publisher, "viewer-1");
        await ReadyAsync(publisher, "viewer-2");
        var frame = new WebRtcAudioFrame([1, 2, 3, 4], 48_000, 2, TimeSpan.Zero);

        await publisher.PushAudioFrameAsync(frame);
        context.Factory.Peers[1].SetDiagnostics(PeerConnectionState.Connected, "relay/udp", TimeSpan.FromMilliseconds(24));

        Assert.All(context.Factory.Peers, peer => Assert.Same(frame, Assert.Single(peer.Frames)));
        Assert.Equal(2, publisher.Diagnostics.ViewerConnectionCount);
        var viewer = publisher.Diagnostics.Viewers.Single(item => item.ViewerId == "viewer-2");
        Assert.Equal(PeerConnectionState.Connected, viewer.State);
        Assert.Equal("relay/udp", viewer.SelectedCandidatePair);
        Assert.Equal(TimeSpan.FromMilliseconds(24), viewer.EstimatedRoundTripTime);
    }

    [Fact]
    public async Task ViewerAndSessionLifecycleDisposeTheCorrectPeers()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;
        await ReadyAsync(publisher, "viewer-1");
        await ReadyAsync(publisher, "viewer-2");

        await publisher.HandleAsync(new(SignalingMessageTypes.SessionLeft, "session-1", "viewer-1"));

        Assert.True(context.Factory.Peers[0].Disposed);
        Assert.False(context.Factory.Peers[1].Disposed);
        Assert.Equal(1, publisher.Diagnostics.ViewerConnectionCount);

        await publisher.HandleAsync(new(SignalingMessageTypes.SessionEnded, "session-1"));

        Assert.True(context.Factory.Peers[1].Disposed);
        Assert.Equal(0, publisher.Diagnostics.ViewerConnectionCount);
    }

    [Fact]
    public async Task InvalidInboundPayloadUpdatesLastError()
    {
        var context = CreateContext();
        await using var publisher = context.Publisher;
        await ReadyAsync(publisher, "viewer-1");

        await Assert.ThrowsAsync<WebRtcPublisherException>(() => publisher.HandleAsync(
            new(SignalingMessageTypes.WebRtcAnswer, "session-1", "viewer-1", JsonSerializer.SerializeToElement(new { wrong = true }))));

        Assert.NotNull(publisher.Diagnostics.LastError);
    }

    [Fact]
    public async Task OfferFailureRemovesPartiallyRegisteredViewer()
    {
        var context = CreateContext();
        context.Factory.CreateOfferException = new InvalidOperationException("offer failed");
        await using var publisher = context.Publisher;

        await Assert.ThrowsAsync<WebRtcPublisherException>(() => ReadyAsync(publisher, "viewer-1"));

        Assert.True(Assert.Single(context.Factory.Peers).Disposed);
        Assert.Equal(0, publisher.Diagnostics.ViewerConnectionCount);
        Assert.Contains("offer failed", publisher.Diagnostics.LastError, StringComparison.Ordinal);
    }

    private static async Task ReadyAsync(WebRtcPublisher publisher, string viewerId) =>
        await publisher.HandleAsync(new(SignalingMessageTypes.ViewerReady, "session-1", viewerId));

    private static TestContext CreateContext()
    {
        var signaling = new RecordingSignalingClient();
        var factory = new FakePeerConnectionFactory();
        var manager = new PeerConnectionManager(factory, new WebRtcPublisherOptions());
        return new(signaling, factory, new WebRtcPublisher(signaling, manager));
    }

    private sealed record TestContext(
        RecordingSignalingClient Signaling,
        FakePeerConnectionFactory Factory,
        WebRtcPublisher Publisher);

    private sealed class RecordingSignalingClient : ISignalingClient
    {
        public List<SignalingMessageEnvelope> Messages { get; } = [];
        public SignalingConnectionState State => SignalingConnectionState.Connected;
        public event Action<SignalingConnectionState>? StateChanged { add { } remove { } }
        public Task ConnectAsync(string sessionId, string deviceId, CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task CloseAsync(CancellationToken cancellationToken = default) => Task.CompletedTask;
        public Task SendAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    private sealed class FakePeerConnectionFactory : IWebRtcPeerConnectionFactory
    {
        public List<FakePeerConnection> Peers { get; } = [];
        public Exception? CreateOfferException { get; set; }
        public Task<IWebRtcPeerConnection> CreateAsync(string viewerId, WebRtcPublisherOptions options, CancellationToken cancellationToken = default)
        {
            var peer = new FakePeerConnection(viewerId) { CreateOfferException = CreateOfferException };
            Peers.Add(peer);
            return Task.FromResult<IWebRtcPeerConnection>(peer);
        }
    }

    private sealed class FakePeerConnection(string viewerId) : IWebRtcPeerConnection
    {
        public string ViewerId { get; } = viewerId;
        public int OfferCount { get; private set; }
        public WebRtcSessionDescription? Answer { get; private set; }
        public List<WebRtcIceCandidate> RemoteCandidates { get; } = [];
        public List<WebRtcAudioFrame> Frames { get; } = [];
        public PeerConnectionDiagnostics Diagnostics { get; private set; } = new(viewerId, PeerConnectionState.New);
        public bool Disposed { get; private set; }
        public Exception? CreateOfferException { get; init; }
        public event Func<WebRtcIceCandidate, CancellationToken, Task>? LocalIceCandidateReady;
        public event Action? DiagnosticsChanged;

        public Task<WebRtcSessionDescription> CreateOfferAsync(CancellationToken cancellationToken = default)
        {
            if (CreateOfferException is not null) return Task.FromException<WebRtcSessionDescription>(CreateOfferException);
            OfferCount++;
            return Task.FromResult(new WebRtcSessionDescription("offer", $"offer-{ViewerId}"));
        }
        public Task ApplyAnswerAsync(WebRtcSessionDescription answer, CancellationToken cancellationToken = default)
        {
            Answer = answer;
            return Task.CompletedTask;
        }
        public Task AddRemoteIceCandidateAsync(WebRtcIceCandidate candidate, CancellationToken cancellationToken = default)
        {
            RemoteCandidates.Add(candidate);
            return Task.CompletedTask;
        }
        public Task SendAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default)
        {
            Frames.Add(frame);
            return Task.CompletedTask;
        }
        public async Task EmitCandidateAsync(WebRtcIceCandidate candidate)
        {
            if (LocalIceCandidateReady is not null) await LocalIceCandidateReady(candidate, CancellationToken.None);
        }
        public void SetDiagnostics(PeerConnectionState state, string pair, TimeSpan rtt)
        {
            Diagnostics = new(ViewerId, state, pair, rtt);
            DiagnosticsChanged?.Invoke();
        }
        public ValueTask DisposeAsync()
        {
            Disposed = true;
            return ValueTask.CompletedTask;
        }
    }
}
