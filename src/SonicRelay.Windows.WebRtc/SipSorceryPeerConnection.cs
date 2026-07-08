using Concentus.Enums;
using Concentus.Structs;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using SonicRelay.Windows.Core.Audio;

namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Publisher-side peer connection backed by SIPSorcery: one send-only Opus
/// 48 kHz audio track per viewer, encoded per the selected
/// <see cref="AudioQualityProfile"/> (channels/bitrate/frame duration). Trickle
/// ICE — local candidates are surfaced through <see cref="LocalIceCandidateReady"/>
/// as they gather and remote ones can be applied at any time after the offer.
/// </summary>
public sealed class SipSorceryPeerConnection : IWebRtcPeerConnection
{
    private const int SampleRate = 48000;

    private readonly RTCPeerConnection connection;
    private readonly OpusEncoder opusEncoder;
    private readonly OpusFrameAccumulator accumulator;
    private readonly SemaphoreSlim sendLock = new(1, 1);
    private readonly AudioFormat opusFormat;
    private readonly byte[] encodeBuffer = new byte[4000];
    // Samples per channel in one frame at 48 kHz; the accumulator emits exactly this.
    private readonly int samplesPerChannel;
    private volatile bool formatNegotiated;
    private PeerConnectionState state = PeerConnectionState.New;
    private bool disposed;

    public SipSorceryPeerConnection(
        string viewerId,
        RTCPeerConnection connection,
        AudioQualityProfile? profile = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(viewerId);
        ViewerId = viewerId;
        this.connection = connection ?? throw new ArgumentNullException(nameof(connection));

        var quality = profile ?? AudioQualityProfile.Default;
        quality.Validate();
        var channels = quality.Channels;
        var bitrate = quality.OpusBitrateKbps * 1000;
        var stereo = channels == 2 ? 1 : 0;
        samplesPerChannel = SampleRate * quality.FrameDurationMs / 1000;
        accumulator = new OpusFrameAccumulator(SampleRate, channels, quality.FrameDurationMs);

        // Advertise Opus with explicit channel/bitrate hints. Without the stereo and
        // maxaveragebitrate fmtp params the remote negotiates a low-bitrate mono
        // profile that sounds muffled; the encoder below is configured to match.
        opusFormat = new AudioFormat(
            AudioCodecsEnum.OPUS,
            111,
            SampleRate,
            channels,
            $"useinbandfec=1;stereo={stereo};sprop-stereo={stereo};maxaveragebitrate={bitrate};maxplaybackrate=48000");
        this.connection.addTrack(new MediaStreamTrack(opusFormat, MediaStreamStatusEnum.SendOnly));

        // Stereo profiles favour music fidelity; mono profiles are tuned for voice.
        var application = channels == 2
            ? OpusApplication.OPUS_APPLICATION_AUDIO
            : OpusApplication.OPUS_APPLICATION_VOIP;
        opusEncoder = new OpusEncoder(SampleRate, channels, application)
        {
            Bitrate = bitrate,
            Complexity = 10,
            SignalType = channels == 2 ? OpusSignal.OPUS_SIGNAL_MUSIC : OpusSignal.OPUS_SIGNAL_VOICE,
        };

        this.connection.OnAudioFormatsNegotiated += OnAudioFormatsNegotiated;
        this.connection.onicecandidate += OnIceCandidate;
        this.connection.onconnectionstatechange += OnConnectionStateChanged;
    }

    public string ViewerId { get; }

    public PeerConnectionDiagnostics Diagnostics => new(ViewerId, state);

    public event Func<WebRtcIceCandidate, CancellationToken, Task>? LocalIceCandidateReady;
    public event Action? DiagnosticsChanged;

    public async Task<WebRtcSessionDescription> CreateOfferAsync(CancellationToken cancellationToken = default)
    {
        ThrowIfDisposed();
        var offer = connection.createOffer(null)
            ?? throw new WebRtcPublisherException("SIPSorcery could not create an SDP offer.");
        await connection.setLocalDescription(offer).ConfigureAwait(false);
        return new WebRtcSessionDescription("offer", offer.sdp);
    }

    public Task ApplyAnswerAsync(WebRtcSessionDescription answer, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(answer);
        ThrowIfDisposed();
        var result = connection.setRemoteDescription(new RTCSessionDescriptionInit
        {
            type = RTCSdpType.answer,
            sdp = answer.Sdp
        });
        return result == SetDescriptionResultEnum.OK
            ? Task.CompletedTask
            : throw new WebRtcPublisherException($"The WebRTC answer was rejected: {result}.");
    }

    public Task AddRemoteIceCandidateAsync(WebRtcIceCandidate candidate, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(candidate);
        ThrowIfDisposed();
        try
        {
            connection.addIceCandidate(new RTCIceCandidateInit
            {
                candidate = candidate.Candidate,
                sdpMid = candidate.SdpMid,
                sdpMLineIndex = (ushort)(candidate.SdpMLineIndex ?? 0)
            });
        }
        catch (Exception exception)
        {
            throw new WebRtcPublisherException("The remote ICE candidate could not be applied.", exception);
        }
        return Task.CompletedTask;
    }

    public async Task SendAudioFrameAsync(WebRtcAudioFrame frame, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(frame);
        if (disposed) return;
        await sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (disposed) return;
            if (state != PeerConnectionState.Connected || !formatNegotiated)
            {
                // No point queueing audio the transport cannot carry yet; stale
                // buffered samples would only add latency once it connects.
                accumulator.Clear();
                return;
            }

            var samples = PcmAudioConverter.ToS16(frame.Data.Span, WebRtcSourceSampleFormat.Pcm16);
            accumulator.Append(samples, frame.SampleRate, frame.ChannelCount);
            while (accumulator.TryTakeFrame(out var pcm))
            {
                var length = opusEncoder.Encode(pcm, samplesPerChannel, encodeBuffer, encodeBuffer.Length);
                if (length <= 0) continue;
                // Opus RTP timestamps advance on the 48 kHz clock: samplesPerChannel
                // units per frame (480/960/1920 for 10/20/40 ms) regardless of channels.
                connection.SendAudio((uint)samplesPerChannel, encodeBuffer[..length]);
            }
        }
        catch (Exception exception) when (exception is not OperationCanceledException)
        {
            throw new WebRtcPublisherException("Sending audio over the peer connection failed.", exception);
        }
        finally
        {
            sendLock.Release();
        }
    }

    private void OnAudioFormatsNegotiated(List<AudioFormat> formats)
    {
        // Gate sending until the remote has accepted Opus; we always encode Opus
        // ourselves, so only the fact of negotiation matters, not the returned format.
        if (formats.Any(format => string.Equals(format.FormatName, "OPUS", StringComparison.OrdinalIgnoreCase)))
        {
            formatNegotiated = true;
        }
    }

    private void OnIceCandidate(RTCIceCandidate? candidate)
    {
        if (candidate is null || string.IsNullOrWhiteSpace(candidate.candidate)) return;
        var handlers = LocalIceCandidateReady;
        if (handlers is null) return;
        // Browsers and flutter_webrtc expect the standard "candidate:" prefix
        // that SIPSorcery omits from RTCIceCandidate.candidate.
        var value = candidate.candidate.StartsWith("candidate:", StringComparison.OrdinalIgnoreCase)
            ? candidate.candidate
            : $"candidate:{candidate.candidate}";
        var sdpMid = string.IsNullOrEmpty(candidate.sdpMid) ? null : candidate.sdpMid;
        var payload = new WebRtcIceCandidate(value, sdpMid, candidate.sdpMLineIndex);
        _ = DispatchCandidateAsync(handlers, payload);
    }

    private static async Task DispatchCandidateAsync(
        Func<WebRtcIceCandidate, CancellationToken, Task> handlers,
        WebRtcIceCandidate candidate)
    {
        foreach (Func<WebRtcIceCandidate, CancellationToken, Task> handler in handlers.GetInvocationList())
        {
            try
            {
                await handler(candidate, CancellationToken.None).ConfigureAwait(false);
            }
            catch
            {
                // Candidate delivery is best-effort; a failed send must not tear
                // down ICE gathering. Connectivity failures surface via the
                // connection state instead.
            }
        }
    }

    private void OnConnectionStateChanged(RTCPeerConnectionState next)
    {
        state = next switch
        {
            RTCPeerConnectionState.@new => PeerConnectionState.New,
            RTCPeerConnectionState.connecting => PeerConnectionState.Connecting,
            RTCPeerConnectionState.connected => PeerConnectionState.Connected,
            RTCPeerConnectionState.disconnected => PeerConnectionState.Disconnected,
            RTCPeerConnectionState.failed => PeerConnectionState.Failed,
            RTCPeerConnectionState.closed => PeerConnectionState.Closed,
            _ => state
        };
        DiagnosticsChanged?.Invoke();
    }

    private void ThrowIfDisposed() => ObjectDisposedException.ThrowIf(disposed, this);

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        await sendLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (disposed) return;
            disposed = true;
        }
        finally
        {
            sendLock.Release();
        }

        connection.OnAudioFormatsNegotiated -= OnAudioFormatsNegotiated;
        connection.onicecandidate -= OnIceCandidate;
        connection.onconnectionstatechange -= OnConnectionStateChanged;
        try
        {
            connection.close();
        }
        catch
        {
            // Closing an already-failed transport must not throw out of dispose.
        }
        sendLock.Dispose();
    }
}
