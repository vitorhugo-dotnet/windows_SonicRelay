using SonicRelay.Windows.Core.Audio;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class OpusEncoderFactoryTests
{
    [Fact]
    public void HighQualityProfileKeepsMusicSettingsWithExplicitResilience()
    {
        var encoder = OpusEncoderFactory.Create(AudioQualityProfile.High);

        Assert.Equal(128000, encoder.Bitrate);
        Assert.True(encoder.UseVBR);
        Assert.True(encoder.UseConstrainedVBR);
        Assert.False(encoder.UseDTX);
        // Explicitly enabled even though the stereo/music profile runs in CELT
        // mode, where LBRR yields no redundancy — see OpusEncoderFactory docs.
        Assert.True(encoder.UseInbandFEC);
        Assert.Equal(AudioQualityProfile.DefaultExpectedPacketLossPercent, encoder.PacketLossPercent);
    }

    [Fact]
    public void VoiceProfileActsAsTheResilientProfileWithFecActive()
    {
        // Mono / 32 kbps / 20 ms / VOIP: the SILK-capable configuration where Opus
        // in-band FEC actually produces redundancy.
        var encoder = OpusEncoderFactory.Create(AudioQualityProfile.Voice);

        Assert.Equal(32000, encoder.Bitrate);
        Assert.True(encoder.UseInbandFEC);
        Assert.True(encoder.UseVBR);
        Assert.True(encoder.UseConstrainedVBR);
        Assert.False(encoder.UseDTX);
    }

    [Fact]
    public void ConfiguredExpectedPacketLossReachesTheEncoder()
    {
        var profile = AudioQualityProfile.Voice with { ExpectedPacketLossPercent = 25 };

        var encoder = OpusEncoderFactory.Create(profile);

        Assert.Equal(25, encoder.PacketLossPercent);
    }

    [Fact]
    public void OutOfRangeExpectedPacketLossIsRejected()
    {
        var profile = AudioQualityProfile.Voice with { ExpectedPacketLossPercent = 101 };

        Assert.Throws<ArgumentException>(() => OpusEncoderFactory.Create(profile));
    }
}
