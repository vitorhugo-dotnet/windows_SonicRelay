using Concentus.Enums;
using Concentus.Structs;
using SIPSorcery.Net;
using SonicRelay.Windows.Core.Audio;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class SipSorceryPeerConnectionTests
{
    [Fact]
    public void OpusMusicEncoderProducesFullbandStereoPackets()
    {
        // Mirrors the production encoder config; proves the Concentus 20 ms stereo
        // encode path yields a non-trivial Opus packet at the music bitrate.
        var encoder = new OpusEncoder(48000, 2, OpusApplication.OPUS_APPLICATION_AUDIO)
        {
            Bitrate = 128000,
            Complexity = 10,
            SignalType = OpusSignal.OPUS_SIGNAL_MUSIC,
        };
        var pcm = new short[960 * 2];
        for (var i = 0; i < 960; i++)
        {
            var sample = (short)(short.MaxValue * 0.5 * Math.Sin(2 * Math.PI * 1000 * i / 48000.0));
            pcm[i * 2] = sample;
            pcm[i * 2 + 1] = sample;
        }
        var buffer = new byte[4000];

        var length = encoder.Encode(pcm, 960, buffer, buffer.Length);

        Assert.True(length > 0, "Opus encode should produce a packet.");
    }

    [Fact]
    public async Task CreateOffer_produces_sendonly_opus_sdp()
    {
        await using var peer = new SipSorceryPeerConnection("viewer-1", new RTCPeerConnection(new RTCConfiguration()));

        var offer = await peer.CreateOfferAsync();

        Assert.Equal("offer", offer.Type);
        Assert.Contains("OPUS", offer.Sdp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a=sendonly", offer.Sdp, StringComparison.Ordinal);
        // Fullband stereo music profile — without these the remote negotiates a
        // muffled low-bitrate mono Opus stream.
        Assert.Contains("stereo=1", offer.Sdp, StringComparison.Ordinal);
        Assert.Contains("maxaveragebitrate=128000", offer.Sdp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Voice_profile_offers_mono_opus_at_its_bitrate()
    {
        await using var peer = new SipSorceryPeerConnection(
            "viewer-1", new RTCPeerConnection(new RTCConfiguration()), AudioQualityProfile.Voice);

        var offer = await peer.CreateOfferAsync();

        Assert.Contains("OPUS", offer.Sdp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("stereo=0", offer.Sdp, StringComparison.Ordinal);
        // Voice preset is 32 kbps.
        Assert.Contains("maxaveragebitrate=32000", offer.Sdp, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Local_ice_candidates_are_emitted_with_the_standard_prefix()
    {
        await using var peer = new SipSorceryPeerConnection("viewer-1", new RTCPeerConnection(new RTCConfiguration()));
        var candidate = new TaskCompletionSource<WebRtcIceCandidate>(TaskCreationOptions.RunContinuationsAsynchronously);
        peer.LocalIceCandidateReady += (c, _) => { candidate.TrySetResult(c); return Task.CompletedTask; };

        await peer.CreateOfferAsync();

        var received = await candidate.Task.WaitAsync(TimeSpan.FromSeconds(10));
        Assert.StartsWith("candidate:", received.Candidate, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Applying_a_malformed_answer_throws_publisher_exception()
    {
        await using var peer = new SipSorceryPeerConnection("viewer-1", new RTCPeerConnection(new RTCConfiguration()));
        await peer.CreateOfferAsync();

        await Assert.ThrowsAsync<WebRtcPublisherException>(
            () => peer.ApplyAnswerAsync(new WebRtcSessionDescription("answer", "not-valid-sdp")));
    }
}
