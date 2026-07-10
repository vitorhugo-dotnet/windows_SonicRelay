using Concentus.Enums;
using Concentus.Structs;
using SonicRelay.Windows.Core.Audio;

namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Builds the Concentus Opus encoder for an <see cref="AudioQualityProfile"/> with
/// the packet-loss resilience options set explicitly. Advertising
/// <c>useinbandfec=1</c> in the SDP only tells the remote decoder that redundancy
/// may be present — the encoder must be configured separately, which this factory
/// does.
///
/// Applicability caveat: Opus in-band FEC (LBRR) is produced only by the
/// SILK/hybrid coder, i.e. the mono voice-oriented profiles (such as the
/// "voice" preset — mono, 32 kbps, VOIP application — which doubles as the
/// resilient profile for lossy networks). High-bitrate stereo music profiles run
/// in CELT mode, where the flag is accepted but yields little or no redundancy;
/// those profiles rely on receiver-side concealment (PLC) plus the publisher's
/// pacing and bounded buffering instead. Enabling it everywhere is still correct:
/// it is a no-op where inapplicable and takes effect if the encoder falls back to
/// SILK/hybrid at low bitrates.
/// </summary>
public static class OpusEncoderFactory
{
    public const int SampleRate = 48000;

    public static OpusEncoder Create(AudioQualityProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        var channels = profile.Channels;
        // Stereo profiles favour music fidelity; mono profiles are tuned for voice.
        var application = channels == 2
            ? OpusApplication.OPUS_APPLICATION_AUDIO
            : OpusApplication.OPUS_APPLICATION_VOIP;
        return new OpusEncoder(SampleRate, channels, application)
        {
            Bitrate = profile.OpusBitrateKbps * 1000,
            Complexity = 10,
            SignalType = channels == 2 ? OpusSignal.OPUS_SIGNAL_MUSIC : OpusSignal.OPUS_SIGNAL_VOICE,
            // Constrained VBR keeps quality benefits while bounding the bitrate
            // spikes that aggravate loss on congested links.
            UseVBR = true,
            UseConstrainedVBR = true,
            // DTX suppresses packets during silence; for system-audio relay that
            // reads as a dropout at the receiver, so it stays off.
            UseDTX = false,
            UseInbandFEC = true,
            PacketLossPercent = profile.ExpectedPacketLossPercent,
        };
    }
}
