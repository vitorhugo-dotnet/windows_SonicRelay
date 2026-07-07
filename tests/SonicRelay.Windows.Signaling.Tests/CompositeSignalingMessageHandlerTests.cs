using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.Signaling.Tests;

public sealed class CompositeSignalingMessageHandlerTests
{
    [Fact]
    public async Task Dispatches_to_all_registered_handlers_in_order()
    {
        var composite = new CompositeSignalingMessageHandler();
        var order = new List<string>();
        composite.Register(new RecordingHandler("first", order));
        composite.Register(new RecordingHandler("second", order));

        await composite.HandleAsync(new SignalingMessageEnvelope("ping"));

        Assert.Equal(["first", "second"], order);
    }

    [Fact]
    public async Task Handlers_registered_after_construction_receive_messages()
    {
        var composite = new CompositeSignalingMessageHandler();
        var order = new List<string>();
        await composite.HandleAsync(new SignalingMessageEnvelope("ping")); // no handlers yet
        composite.Register(new RecordingHandler("late", order));

        await composite.HandleAsync(new SignalingMessageEnvelope("ping"));

        Assert.Equal(["late"], order);
    }

    [Fact]
    public async Task Passes_cancellation_token_through()
    {
        var composite = new CompositeSignalingMessageHandler();
        using var cts = new CancellationTokenSource();
        CancellationToken? seen = null;
        composite.Register(new DelegateHandler((_, token) => { seen = token; return Task.CompletedTask; }));

        await composite.HandleAsync(new SignalingMessageEnvelope("ping"), cts.Token);

        Assert.Equal(cts.Token, seen);
    }

    private sealed class RecordingHandler(string name, List<string> order) : ISignalingMessageHandler
    {
        public Task HandleAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default)
        {
            order.Add(name);
            return Task.CompletedTask;
        }
    }

    private sealed class DelegateHandler(Func<SignalingMessageEnvelope, CancellationToken, Task> handle) : ISignalingMessageHandler
    {
        public Task HandleAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default) =>
            handle(message, cancellationToken);
    }
}
