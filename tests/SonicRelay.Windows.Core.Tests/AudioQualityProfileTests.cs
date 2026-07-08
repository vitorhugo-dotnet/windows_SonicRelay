using SonicRelay.Windows.Core.Audio;

namespace SonicRelay.Windows.Core.Tests;

public sealed class AudioQualityProfileTests
{
    [Fact]
    public void Presets_have_the_specified_values()
    {
        Assert.Equal(new AudioQualityProfile("voice", "Voice / Economy", 1, 32, 20, 48000), AudioQualityProfile.Voice);
        Assert.Equal(new AudioQualityProfile("balanced", "Balanced", 2, 96, 20, 48000), AudioQualityProfile.Balanced);
        Assert.Equal(new AudioQualityProfile("high", "High quality", 2, 128, 20, 48000), AudioQualityProfile.High);
        Assert.Same(AudioQualityProfile.High, AudioQualityProfile.Default);
    }

    [Fact]
    public void All_presets_validate()
    {
        foreach (var preset in AudioQualityProfile.Presets)
        {
            preset.Validate();
        }
    }

    [Theory]
    [InlineData(0)]   // below range
    [InlineData(15)]
    [InlineData(193)] // above range
    public void Validate_rejects_out_of_range_bitrate(int bitrate)
    {
        var profile = AudioQualityProfile.High with { OpusBitrateKbps = bitrate };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(3)]
    public void Validate_rejects_invalid_channel_count(int channels)
    {
        var profile = AudioQualityProfile.High with { Channels = channels };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Theory]
    [InlineData(5)]
    [InlineData(15)]
    [InlineData(60)]
    public void Validate_rejects_invalid_frame_duration(int frameMs)
    {
        var profile = AudioQualityProfile.High with { FrameDurationMs = frameMs };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void Validate_rejects_non_48k_sample_rate()
    {
        var profile = AudioQualityProfile.High with { SampleRateHz = 44100 };
        Assert.Throws<ArgumentException>(profile.Validate);
    }

    [Fact]
    public void Custom_builds_a_validated_custom_profile()
    {
        var profile = AudioQualityProfile.Custom(channels: 1, bitrateKbps: 64, frameDurationMs: 40);

        Assert.Equal("custom", profile.Id);
        Assert.Equal(1, profile.Channels);
        Assert.Equal(64, profile.OpusBitrateKbps);
        Assert.Equal(40, profile.FrameDurationMs);
        Assert.Equal(48000, profile.SampleRateHz);
    }

    [Fact]
    public void Custom_rejects_invalid_arguments()
    {
        Assert.Throws<ArgumentException>(() => AudioQualityProfile.Custom(2, 500, 20));
    }

    [Theory]
    [InlineData("voice")]
    [InlineData("balanced")]
    [InlineData("high")]
    public void FromId_resolves_presets(string id)
    {
        Assert.Equal(id, AudioQualityProfile.FromId(id)!.Id);
    }

    [Theory]
    [InlineData("custom")]
    [InlineData("nonsense")]
    public void FromId_returns_null_for_non_presets(string id)
    {
        Assert.Null(AudioQualityProfile.FromId(id));
    }

    [Fact]
    public void EstimateTraffic_derives_from_the_opus_bitrate()
    {
        // 96 kbps => 12 000 bytes/s => 0.72 MB/min => 43.2 MB/hour.
        var estimate = AudioQualityProfile.Balanced.EstimateTraffic();

        Assert.Equal(96, estimate.Kbps);
        Assert.Equal(0.72, estimate.MegabytesPerMinute, precision: 3);
        Assert.Equal(43.2, estimate.MegabytesPerHour, precision: 3);
    }
}
