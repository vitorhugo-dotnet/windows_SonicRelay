using SIPSorcery.Net;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class SipSorceryPeerConnectionTests
{
    [Fact]
    public async Task CreateOffer_produces_sendonly_opus_sdp()
    {
        await using var peer = new SipSorceryPeerConnection("viewer-1", new RTCPeerConnection(new RTCConfiguration()));

        var offer = await peer.CreateOfferAsync();

        Assert.Equal("offer", offer.Type);
        Assert.Contains("OPUS", offer.Sdp, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("a=sendonly", offer.Sdp, StringComparison.Ordinal);
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
