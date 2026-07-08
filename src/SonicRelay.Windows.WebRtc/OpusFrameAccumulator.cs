namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Buffers ragged capture packets and emits exact frames of the configured
/// duration (default 20 ms → 960 samples per channel at 48 kHz) at the encoder's
/// target rate/channel layout. Handles mono/stereo up- and down-mixing and linear
/// resampling of arbitrary common source rates (44.1 kHz WASAPI mixes in
/// particular). Not thread-safe; callers serialize access.
/// </summary>
public sealed class OpusFrameAccumulator
{
    private readonly int targetSampleRate;
    private readonly int targetChannels;
    private readonly int frameDurationMs;
    private readonly List<short> buffer = [];
    private int sourceSampleRate;
    private int sourceSamplesPerFramePerChannel;

    public OpusFrameAccumulator(int targetSampleRate = 48000, int targetChannels = 2, int frameDurationMs = 20)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(targetSampleRate);
        if (targetChannels is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(targetChannels));
        if (frameDurationMs is not (10 or 20 or 40))
            throw new ArgumentOutOfRangeException(nameof(frameDurationMs), "Frame duration must be 10, 20, or 40 ms.");
        this.targetSampleRate = targetSampleRate;
        this.targetChannels = targetChannels;
        this.frameDurationMs = frameDurationMs;
    }

    private int TargetSamplesPerFramePerChannel => targetSampleRate * frameDurationMs / 1000;

    public int TargetFrameSize => TargetSamplesPerFramePerChannel * targetChannels;

    /// <summary>Appends interleaved S16 samples captured at the given rate/channel count.</summary>
    public void Append(ReadOnlySpan<short> samples, int sampleRate, int channelCount)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        if (sampleRate * frameDurationMs % 1000 != 0)
            throw new ArgumentException(
                $"Sample rate must yield a whole number of samples per {frameDurationMs} ms frame.", nameof(sampleRate));
        if (channelCount is < 1 or > 2) throw new ArgumentOutOfRangeException(nameof(channelCount));
        if (sampleRate != sourceSampleRate)
        {
            // Source format changed (e.g. new default device); drop stale samples.
            buffer.Clear();
            sourceSampleRate = sampleRate;
            sourceSamplesPerFramePerChannel = sampleRate * frameDurationMs / 1000;
        }

        if (channelCount == targetChannels)
        {
            AppendRaw(samples);
        }
        else if (channelCount == 1)
        {
            for (var i = 0; i < samples.Length; i++)
            {
                buffer.Add(samples[i]);
                buffer.Add(samples[i]);
            }
        }
        else
        {
            for (var i = 0; i + 1 < samples.Length; i += 2)
            {
                buffer.Add((short)((samples[i] + samples[i + 1]) / 2));
            }
        }
    }

    /// <summary>
    /// Takes one 20 ms frame at the target rate when enough source samples are
    /// buffered. The frame length is <see cref="TargetFrameSize"/>.
    /// </summary>
    public bool TryTakeFrame(out short[] frame)
    {
        frame = [];
        if (sourceSampleRate == 0) return false;
        var neededSourceSamples = sourceSamplesPerFramePerChannel * targetChannels;
        if (buffer.Count < neededSourceSamples) return false;

        var source = buffer.GetRange(0, neededSourceSamples).ToArray();
        buffer.RemoveRange(0, neededSourceSamples);

        if (sourceSampleRate == targetSampleRate)
        {
            frame = source;
            return true;
        }

        frame = ResampleInterleaved(
            source, sourceSamplesPerFramePerChannel, TargetSamplesPerFramePerChannel, targetChannels);
        return true;
    }

    public void Clear()
    {
        buffer.Clear();
        sourceSampleRate = 0;
    }

    private void AppendRaw(ReadOnlySpan<short> samples)
    {
        buffer.EnsureCapacity(buffer.Count + samples.Length);
        foreach (var sample in samples) buffer.Add(sample);
    }

    private static short[] ResampleInterleaved(short[] source, int sourceFrames, int targetFrames, int channels)
    {
        var result = new short[targetFrames * channels];
        for (var frameIndex = 0; frameIndex < targetFrames; frameIndex++)
        {
            var position = (double)frameIndex * (sourceFrames - 1) / Math.Max(targetFrames - 1, 1);
            var lower = (int)position;
            var upper = Math.Min(lower + 1, sourceFrames - 1);
            var fraction = position - lower;
            for (var channel = 0; channel < channels; channel++)
            {
                var a = source[lower * channels + channel];
                var b = source[upper * channels + channel];
                result[frameIndex * channels + channel] = (short)Math.Round(a + (b - a) * fraction);
            }
        }
        return result;
    }
}
