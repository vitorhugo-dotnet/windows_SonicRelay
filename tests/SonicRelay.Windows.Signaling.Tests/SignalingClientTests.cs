using SonicRelay.Windows.Core.Configuration;
using SonicRelay.Windows.Core.Storage;
using SonicRelay.Windows.Signaling.WebSockets;
using System.Net;
using System.Net.WebSockets;

namespace SonicRelay.Windows.Signaling.Tests;

public sealed class SignalingClientTests
{
    private static readonly TokenSet Tokens = new("access-secret", "refresh-secret", DateTimeOffset.UtcNow.AddHours(1));

    [Fact]
    public async Task ConnectUsesConfiguredIdentityAndSendsPublisherReady()
    {
        var socket = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(socket);
        await using var client = CreateClient(factory);

        await client.ConnectAsync("session one", "device/two");

        Assert.Equal("wss://signal.example/ws?tenant=blue&sessionId=session%20one&deviceId=device%2Ftwo", socket.ConnectedUri?.AbsoluteUri);
        Assert.Equal("access-secret", socket.AccessToken);
        Assert.Equal(SignalingConnectionState.Connected, client.State);
        Assert.Equal(SignalingMessageTypes.PublisherReady, SignalingMessageEnvelope.Deserialize(Assert.Single(socket.Sent)).Type);
    }

    [Fact]
    public async Task ReceiveDispatchesViewerReadyAndAnswersPing()
    {
        var socket = new FakeWebSocketConnection();
        var handler = new RecordingHandler();
        await using var client = CreateClient(new FakeWebSocketFactory(socket), handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        socket.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.ViewerReady, "session-1", From: "viewer-7"));
        var dispatched = await handler.NextAsync(timeout.Token);
        socket.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.Ping, "session-1"));
        await WaitUntilAsync(() => socket.Sent.Count == 2, timeout.Token);

        Assert.Equal("viewer-7", dispatched.From);
        Assert.Equal(SignalingMessageTypes.Pong, SignalingMessageEnvelope.Deserialize(socket.Sent[1]).Type);
    }

    [Fact]
    public async Task InvalidMessageIsIgnoredAndReceiveLoopContinues()
    {
        var socket = new FakeWebSocketConnection();
        var handler = new RecordingHandler();
        await using var client = CreateClient(new FakeWebSocketFactory(socket), handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        socket.QueueText("{not-json}");
        socket.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.ViewerReady, "session-1", From: "viewer-9"));
        var dispatched = await handler.NextAsync(timeout.Token);

        Assert.Equal("viewer-9", dispatched.From);
        Assert.Equal(SignalingConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task ConnectIsIdempotentForSameIdentityAndRejectsAnotherSession()
    {
        var factory = new FakeWebSocketFactory(new FakeWebSocketConnection());
        await using var client = CreateClient(factory);
        await client.ConnectAsync("session-1", "device-1");

        await client.ConnectAsync("session-1", "device-1");
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => client.ConnectAsync("session-2", "device-1"));

        Assert.Equal(1, factory.CreatedCount);
        Assert.Contains("already active", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task SessionEndedDispatchesAndClosesWithoutReconnect()
    {
        var socket = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(socket);
        var handler = new RecordingHandler();
        await using var client = CreateClient(factory, handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        socket.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.SessionEnded, "session-1"));
        await handler.NextAsync(timeout.Token);
        await WaitUntilAsync(() => client.State == SignalingConnectionState.Closed, timeout.Token);

        Assert.Equal(1, factory.CreatedCount);
        Assert.Equal(System.Net.WebSockets.WebSocketState.Closed, socket.State);
    }

    [Fact]
    public async Task ClosePublishesObservableStateTransitions()
    {
        var states = new List<SignalingConnectionState>();
        await using var client = CreateClient(new FakeWebSocketFactory(new FakeWebSocketConnection()));
        client.StateChanged += states.Add;

        await client.ConnectAsync("session-1", "device-1");
        await client.CloseAsync();

        Assert.Equal(
            [SignalingConnectionState.Connecting, SignalingConnectionState.Connected, SignalingConnectionState.Closing, SignalingConnectionState.Closed],
            states);
    }

    [Fact]
    public async Task TransientCloseReconnectsWithBackoffAndSendsPublisherReadyAgain()
    {
        var first = new FakeWebSocketConnection();
        var replacement = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(first, replacement);
        var delay = new ImmediateReconnectDelay();
        var states = new List<SignalingConnectionState>();
        await using var client = CreateClient(factory, delay: delay);
        client.StateChanged += states.Add;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        first.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(() => factory.CreatedCount == 2 && client.State == SignalingConnectionState.Connected, timeout.Token);

        Assert.Equal([TimeSpan.FromSeconds(1)], delay.Delays);
        Assert.Contains(SignalingConnectionState.Reconnecting, states);
        Assert.Equal(SignalingMessageTypes.PublisherReady, SignalingMessageEnvelope.Deserialize(Assert.Single(replacement.Sent)).Type);
    }

    [Fact]
    public async Task ReconnectStopsAfterConfiguredMaxAttempts()
    {
        var initial = new FakeWebSocketConnection();
        var failure = new WebSocketException("transient");
        var factory = new FakeWebSocketFactory(
            initial,
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection { ConnectException = failure });
        var delay = new ImmediateReconnectDelay();
        await using var client = CreateClient(factory, delay: delay,
            policy: new SignalingReconnectPolicy { MaxAttempts = 3 });
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        initial.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(() => client.State == SignalingConnectionState.Faulted, timeout.Token);

        Assert.Equal(4, factory.CreatedCount);
        Assert.Equal([TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4)], delay.Delays);
    }

    [Fact]
    public async Task ReconnectKeepsRetryingPastThreeFailuresByDefault()
    {
        var initial = new FakeWebSocketConnection();
        var failure = new WebSocketException("transient");
        var factory = new FakeWebSocketFactory(
            initial,
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection { ConnectException = failure },
            new FakeWebSocketConnection()); // sixth attempt finally succeeds
        var delay = new ImmediateReconnectDelay();
        await using var client = CreateClient(factory, delay: delay);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        initial.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(
            () => factory.CreatedCount == 6 && client.State == SignalingConnectionState.Connected,
            timeout.Token);

        // Backoff is capped exponential: 1, 2, 4, 8, 16 s across the five retries.
        Assert.Equal(
            [TimeSpan.FromSeconds(1), TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(4), TimeSpan.FromSeconds(8), TimeSpan.FromSeconds(16)],
            delay.Delays);
    }

    [Fact]
    public async Task ReconnectAppliesConfiguredJitterToTheBackoffDelay()
    {
        var initial = new FakeWebSocketConnection();
        var replacement = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(initial, replacement);
        var delay = new ImmediateReconnectDelay();
        await using var client = CreateClient(factory, delay: delay,
            policy: new SignalingReconnectPolicy { JitterRatio = 0.5 },
            jitter: new FixedReconnectJitter(1));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        initial.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(() => factory.CreatedCount == 2 && client.State == SignalingConnectionState.Connected, timeout.Token);

        // 1s base delay, +50% from a 0.5 jitter ratio scaled by the maximal +1 fixed jitter draw.
        Assert.Equal([TimeSpan.FromSeconds(1.5)], delay.Delays);
    }

    [Fact]
    public async Task ReconnectJitterNeverPushesTheDelayBelowZeroOrAboveMaxDelay()
    {
        var initial = new FakeWebSocketConnection();
        var replacement = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(initial, replacement);
        var delay = new ImmediateReconnectDelay();
        await using var client = CreateClient(factory, delay: delay,
            policy: new SignalingReconnectPolicy { JitterRatio = 1, MaxDelay = TimeSpan.FromSeconds(1) },
            jitter: new FixedReconnectJitter(-1));
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        initial.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(() => factory.CreatedCount == 2 && client.State == SignalingConnectionState.Connected, timeout.Token);

        Assert.Equal([TimeSpan.Zero], delay.Delays);
    }

    [Fact]
    public async Task SessionGoneDuringReconnectStopsRetryingAndReleasesTheSession()
    {
        var initial = new FakeWebSocketConnection();
        var gone = new FakeWebSocketConnection
        {
            ConnectException = new SignalingSessionGoneException(HttpStatusCode.Gone),
        };
        var fresh = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(initial, gone, fresh);
        var delay = new ImmediateReconnectDelay();
        await using var client = CreateClient(factory, delay: delay);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        // The socket drops (transient), and the single reconnect finds the session gone.
        initial.QueueClose(WebSocketCloseStatus.EndpointUnavailable);
        await WaitUntilAsync(() => client.State == SignalingConnectionState.Closed, timeout.Token);

        // Exactly one reconnect attempt was made — no infinite 410 loop.
        Assert.Equal(2, factory.CreatedCount);

        // The identity is released, so a brand-new session starts without the
        // "already active for another session" lock.
        await client.ConnectAsync("session-2", "device-1");
        Assert.Equal(3, factory.CreatedCount);
        Assert.Equal(SignalingConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task PingReplyDoesNotStopTheLoopAndMessagesKeepFlowing()
    {
        var connection = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(connection);
        var handler = new RecordingHandler();
        await using var client = CreateClient(factory, handler: handler);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        connection.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.Ping, "session-1", From: "server"));
        connection.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.ViewerReady, "session-1", To: "publisher"));

        // The ViewerReady message is still dispatched after the ping was handled.
        var received = await handler.NextAsync(timeout.Token);
        while (received.Type == SignalingMessageTypes.Ping) received = await handler.NextAsync(timeout.Token);
        Assert.Equal(SignalingMessageTypes.ViewerReady, received.Type);
        Assert.Equal(SignalingConnectionState.Connected, client.State);
    }

    [Fact]
    public async Task ThrowingHandlerDoesNotStopSubsequentDispatch()
    {
        var connection = new FakeWebSocketConnection();
        var factory = new FakeWebSocketFactory(connection);
        var throwing = new ThrowingHandler();
        var recording = new RecordingHandler();
        var faults = new List<Exception>();
        await using var client = CreateClient(factory, handlers: [throwing, recording]);
        client.HandlerFaulted += faults.Add;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        await client.ConnectAsync("session-1", "device-1");

        connection.QueueText(new SignalingMessageEnvelope(SignalingMessageTypes.ViewerReady, "session-1", To: "publisher"));

        var received = await recording.NextAsync(timeout.Token);
        Assert.Equal(SignalingMessageTypes.ViewerReady, received.Type);
        Assert.Equal(SignalingConnectionState.Connected, client.State);
        Assert.NotEmpty(faults);
    }

    private static SignalingClient CreateClient(
        IWebSocketConnectionFactory factory,
        ISignalingMessageHandler? handler = null,
        IReadOnlyList<ISignalingMessageHandler>? handlers = null,
        IReconnectDelay? delay = null,
        SignalingReconnectPolicy? policy = null,
        IReconnectJitter? jitter = null) =>
        new(
            new PublisherConfiguration(new Uri("https://api.example/"), new Uri("https://signal.example/ws?tenant=blue"), 4),
            new MemoryTokenStore(Tokens),
            handlers ?? (handler is null ? [] : [handler]),
            factory,
            delay ?? new ImmediateReconnectDelay(),
            policy,
            // Deterministic zero jitter by default so the many exact-delay assertions in this
            // file stay stable; tests that care about jitter pass a FixedReconnectJitter.
            jitter ?? new FixedReconnectJitter(0));

    private sealed class FixedReconnectJitter(double ratio) : IReconnectJitter
    {
        public double NextRatio() => ratio;
    }

    private static async Task WaitUntilAsync(Func<bool> condition, CancellationToken cancellationToken)
    {
        while (!condition())
        {
            await Task.Delay(10, cancellationToken);
        }
    }
}
