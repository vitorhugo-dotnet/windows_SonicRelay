using System.Runtime.InteropServices;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class PcmAudioConverterTests
{
    [Fact]
    public void Pcm16_passthrough_reinterprets_bytes_as_samples()
    {
        short[] expected = [0, 1, -1, short.MaxValue, short.MinValue];
        var bytes = MemoryMarshal.AsBytes(expected.AsSpan()).ToArray();

        var result = PcmAudioConverter.ToS16(bytes, WebRtcSourceSampleFormat.Pcm16);

        Assert.Equal(expected, result);
    }

    [Fact]
    public void Float32_is_scaled_to_s16_and_clamped()
    {
        float[] input = [0f, 1f, -1f, 0.5f, 2f, -2f];
        var bytes = MemoryMarshal.AsBytes(input.AsSpan()).ToArray();

        var result = PcmAudioConverter.ToS16(bytes, WebRtcSourceSampleFormat.IeeeFloat32);

        Assert.Equal(0, result[0]);
        Assert.Equal(short.MaxValue, result[1]);
        Assert.Equal(-short.MaxValue, result[2]);
        Assert.Equal((short)Math.Round(0.5 * short.MaxValue), result[3]);
        Assert.Equal(short.MaxValue, result[4]);   // clamped from 2f
        Assert.Equal(-short.MaxValue, result[5]);  // clamped from -2f
    }

    [Fact]
    public void Empty_input_returns_empty()
    {
        Assert.Empty(PcmAudioConverter.ToS16([], WebRtcSourceSampleFormat.Pcm16));
        Assert.Empty(PcmAudioConverter.ToS16([], WebRtcSourceSampleFormat.IeeeFloat32));
    }

    [Fact]
    public void Trailing_partial_sample_bytes_are_ignored()
    {
        // 5 bytes = two whole S16 samples plus one dangling byte.
        var result = PcmAudioConverter.ToS16(new byte[] { 1, 0, 2, 0, 9 }, WebRtcSourceSampleFormat.Pcm16);
        Assert.Equal(new short[] { 1, 2 }, result);
    }
}
