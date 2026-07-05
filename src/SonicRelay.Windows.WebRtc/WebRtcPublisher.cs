using System.Text.Json;
using SonicRelay.Windows.Signaling;

namespace SonicRelay.Windows.WebRtc;

public sealed class WebRtcPublisher : IWebRtcPublisher
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly ISignalingClient signaling;
    private readonly IPeerConnectionManager peers;
    private string? activeSessionId;
    private string? lastError;
    private bool disposed;

    public WebRtcPublisher(ISignalingClient signaling, IPeerConnectionManager peers)
    {
        this.signaling = signaling ?? throw new ArgumentNullException(nameof(signaling));
        this.peers = peers ?? throw new ArgumentNullException(nameof(peers));
        peers.LocalIceCandidateReady += SendLocalIceCandidateAsync;
        peers.DiagnosticsChanged += PublishDiagnostics;
    }

    public WebRtcPublisherDiagnostics Diagnostics =>
        new(peers.ViewerCount, peers.GetDiagnostics(), lastError);

    public event Action<WebRtcPublisherDiagnostics>? DiagnosticsChanged;

    public async Task HandleAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ObjectDisposedException.ThrowIf(disposed, this);
        try
        {
            switch (message.Type)
            {
                case SignalingMessageTypes.ViewerReady:
                    await HandleViewerReadyAsync(message, cancellationToken);
                    break;
                case SignalingMessageTypes.WebRtcAnswer:
                    ValidateSession(message);
                    await peers.ApplyAnswerAsync(RequireViewerId(message), DeserializePayload<WebRtcSessionDescription>(message), cancellationToken);
                    break;
                case SignalingMessageTypes.WebRtcIceCandidate:
                    ValidateSession(message);
                    await peers.AddRemoteIceCandidateAsync(RequireViewerId(message), DeserializePayload<WebRtcIceCandidate>(message), cancellationToken);
                    break;
                case SignalingMessageTypes.SessionLeft when message.ViewerId is not null:
                    ValidateSession(message);
                    await peers.RemoveViewerAsync(message.ViewerId, cancellationToken);
                    break;
                case SignalingMessageTypes.SessionEnded:
                    ValidateSession(message);
                    await peers.RemoveAllAsync(cancellationToken);
                    activeSessionId = null;
                    break;
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            lastError = exception.Message;
            PublishDiagnostics();
            if (exception is WebRtcPublisherException) throw;
            throw new WebRtcPublisherException("WebRTC signaling processing failed.", exception);
        }
    }

    public async Task PushAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default)
    {
        try
        {
            await peers.PushAudioFrameAsync(frame, cancellationToken);
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            lastError = exception.Message;
            PublishDiagnostics();
            throw;
        }
    }

    private async Task HandleViewerReadyAsync(SignalingMessageEnvelope message, CancellationToken cancellationToken)
    {
        var sessionId = RequireSessionId(message);
        if (activeSessionId is null) activeSessionId = sessionId;
        ValidateSession(message);
        var viewerId = RequireViewerId(message);
        var registration = await peers.RegisterViewerAsync(viewerId, cancellationToken);
        if (!registration.WasCreated) return;
        try
        {
            var offer = await registration.Peer.Connection.CreateOfferAsync(cancellationToken);
            await signaling.SendAsync(
                new SignalingMessageEnvelope(
                    SignalingMessageTypes.WebRtcOffer,
                    sessionId,
                    viewerId,
                    JsonSerializer.SerializeToElement(offer, JsonOptions)),
                cancellationToken);
        }
        catch
        {
            await peers.RemoveViewerAsync(viewerId, CancellationToken.None);
            throw;
        }
    }

    private async Task SendLocalIceCandidateAsync(
        string viewerId,
        WebRtcIceCandidate candidate,
        CancellationToken cancellationToken)
    {
        var sessionId = activeSessionId
            ?? throw new WebRtcPublisherException("Cannot send a local ICE candidate without an active session.");
        await signaling.SendAsync(
            new SignalingMessageEnvelope(
                SignalingMessageTypes.WebRtcIceCandidate,
                sessionId,
                viewerId,
                JsonSerializer.SerializeToElement(candidate, JsonOptions)),
            cancellationToken);
    }

    private void ValidateSession(SignalingMessageEnvelope message)
    {
        var sessionId = RequireSessionId(message);
        if (!string.Equals(activeSessionId, sessionId, StringComparison.Ordinal))
        {
            throw new WebRtcPublisherException($"Message session '{sessionId}' does not match the active WebRTC session.");
        }
    }

    private static string RequireSessionId(SignalingMessageEnvelope message) =>
        !string.IsNullOrWhiteSpace(message.SessionId)
            ? message.SessionId
            : throw new WebRtcPublisherException("A signaling session ID is required.");

    private static string RequireViewerId(SignalingMessageEnvelope message) =>
        !string.IsNullOrWhiteSpace(message.ViewerId)
            ? message.ViewerId
            : throw new WebRtcPublisherException("A signaling viewer ID is required.");

    private static T DeserializePayload<T>(SignalingMessageEnvelope message)
    {
        if (message.Payload is null)
        {
            throw new WebRtcPublisherException($"A {typeof(T).Name} payload is required.");
        }
        try
        {
            var payload = message.Payload.Value.Deserialize<T>(JsonOptions)
                ?? throw new WebRtcPublisherException($"The {typeof(T).Name} payload is empty.");
            ValidatePayload(payload);
            return payload;
        }
        catch (JsonException exception)
        {
            throw new WebRtcPublisherException($"The {typeof(T).Name} payload is invalid.", exception);
        }
    }

    private static void ValidatePayload<T>(T payload)
    {
        if (payload is WebRtcSessionDescription description
            && (string.IsNullOrWhiteSpace(description.Type) || string.IsNullOrWhiteSpace(description.Sdp)))
        {
            throw new WebRtcPublisherException("A WebRTC session description requires type and SDP values.");
        }
        if (payload is WebRtcIceCandidate candidate && string.IsNullOrWhiteSpace(candidate.Candidate))
        {
            throw new WebRtcPublisherException("A WebRTC ICE candidate value is required.");
        }
    }

    private void PublishDiagnostics() => DiagnosticsChanged?.Invoke(Diagnostics);

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        peers.LocalIceCandidateReady -= SendLocalIceCandidateAsync;
        peers.DiagnosticsChanged -= PublishDiagnostics;
        await peers.DisposeAsync();
    }
}
