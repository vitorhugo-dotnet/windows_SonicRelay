using SonicRelay.Windows.Audio;

namespace SonicRelay.Windows.Audio.Tests;

public sealed class AudioContractsTests
{
    [Fact]
    public void AudioFrameValidatesRequiredValues()
    {
        Assert.Throws<ArgumentException>(() => new AudioFrame([], 48_000, 2, AudioSampleFormat.IeeeFloat32, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrame([1], 0, 2, AudioSampleFormat.IeeeFloat32, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrame([1], 48_000, 0, AudioSampleFormat.IeeeFloat32, TimeSpan.Zero));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrame([1], 48_000, 2, AudioSampleFormat.IeeeFloat32, TimeSpan.FromTicks(-1)));
    }

    [Fact]
    public void AudioFrameOwnsItsData()
    {
        byte[] source = [1, 2, 3, 4];
        var frame = new AudioFrame(source, 48_000, 2, AudioSampleFormat.Pcm16, TimeSpan.Zero);

        source[0] = 99;

        Assert.Equal(1, frame.Data.Span[0]);
    }

    [Fact]
    public void LevelSnapshotRequiresNormalizedFiniteValues()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioLevelSnapshot(-0.1f, 0.5f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioLevelSnapshot(0.5f, 1.1f));
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioLevelSnapshot(float.NaN, 0.5f));
    }
}
