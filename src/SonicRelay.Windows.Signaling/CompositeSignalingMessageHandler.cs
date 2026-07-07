namespace SonicRelay.Windows.Signaling;

/// <summary>
/// A signaling handler whose delegates can be registered after the
/// <see cref="SignalingClient"/> is constructed. This breaks the chicken-and-egg
/// between the client (which takes its handlers up front) and a handler that
/// needs the client (e.g. the WebRTC publisher, which sends offers/candidates
/// through it). Handlers are dispatched sequentially in registration order.
/// </summary>
public sealed class CompositeSignalingMessageHandler : ISignalingMessageHandler
{
    private readonly List<ISignalingMessageHandler> handlers = [];
    private readonly Lock gate = new();

    public void Register(ISignalingMessageHandler handler)
    {
        ArgumentNullException.ThrowIfNull(handler);
        lock (gate) handlers.Add(handler);
    }

    public async Task HandleAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default)
    {
        ISignalingMessageHandler[] snapshot;
        lock (gate) snapshot = handlers.ToArray();
        foreach (var handler in snapshot)
        {
            await handler.HandleAsync(message, cancellationToken).ConfigureAwait(false);
        }
    }
}
