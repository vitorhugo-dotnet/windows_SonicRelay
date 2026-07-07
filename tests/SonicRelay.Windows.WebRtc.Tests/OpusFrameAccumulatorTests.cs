using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class OpusFrameAccumulatorTests
{
    [Fact]
    public void Emits_exact_20ms_stereo_frames_from_ragged_packets()
    {
        var accumulator = new OpusFrameAccumulator(48000, 2);
        // 960 samples/channel * 2 channels = 1920 shorts per 20 ms frame.
        // Feed three ragged packets that together make two full frames.
        accumulator.Append(MakeStereo(500), 48000, 2);
        Assert.False(accumulator.TryTakeFrame(out _));
        accumulator.Append(MakeStereo(500), 48000, 2);
        accumulator.Append(MakeStereo(200), 48000, 2); // total 1200 frames -> one 960 frame + 240 remainder

        Assert.True(accumulator.TryTakeFrame(out var frame));
        Assert.Equal(1920, frame.Length);
        Assert.False(accumulator.TryTakeFrame(out _));
    }

    [Fact]
    public void Upmixes_mono_to_stereo()
    {
        var accumulator = new OpusFrameAccumulator(48000, 2);
        accumulator.Append(MakeMono(960, 111), 48000, 1);

        Assert.True(accumulator.TryTakeFrame(out var frame));
        Assert.Equal(1920, frame.Length);
        Assert.Equal(111, frame[0]);
        Assert.Equal(111, frame[1]);
    }

    [Fact]
    public void Downmixes_stereo_source_when_target_is_mono()
    {
        var accumulator = new OpusFrameAccumulator(48000, 1);
        var stereo = new short[960 * 2];
        for (var i = 0; i < 960; i++) { stereo[i * 2] = 100; stereo[i * 2 + 1] = 200; }
        accumulator.Append(stereo, 48000, 2);

        Assert.True(accumulator.TryTakeFrame(out var frame));
        Assert.Equal(960, frame.Length);
        Assert.All(frame, sample => Assert.Equal(150, sample));
    }

    [Fact]
    public void Resamples_44100_to_48000_producing_target_length_frames()
    {
        var accumulator = new OpusFrameAccumulator(48000, 2);
        // 44100 Hz -> 20 ms frame = 882 samples/channel of source produces 960/channel target.
        accumulator.Append(MakeStereo(882), 44100, 2);

        Assert.True(accumulator.TryTakeFrame(out var frame));
        Assert.Equal(1920, frame.Length);
    }

    [Fact]
    public void Source_rate_change_discards_stale_buffer()
    {
        var accumulator = new OpusFrameAccumulator(48000, 2);
        accumulator.Append(MakeStereo(400), 48000, 2);
        // A device switch changes the rate before a full frame accumulated.
        accumulator.Append(MakeStereo(882), 44100, 2);

        Assert.True(accumulator.TryTakeFrame(out var frame));
        Assert.Equal(1920, frame.Length);
    }

    [Fact]
    public void Rejects_sample_rate_not_divisible_by_50()
    {
        var accumulator = new OpusFrameAccumulator(48000, 2);
        Assert.Throws<ArgumentException>(() => accumulator.Append(MakeStereo(10), 44101, 2));
    }

    private static short[] MakeStereo(int framesPerChannel)
    {
        var data = new short[framesPerChannel * 2];
        for (var i = 0; i < data.Length; i++) data[i] = (short)(i % 100);
        return data;
    }

    private static short[] MakeMono(int frames, short value)
    {
        var data = new short[frames];
        Array.Fill(data, value);
        return data;
    }
}
