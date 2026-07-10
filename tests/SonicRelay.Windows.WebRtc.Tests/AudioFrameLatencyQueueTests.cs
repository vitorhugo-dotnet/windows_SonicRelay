using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.WebRtc.Tests;

public sealed class AudioFrameLatencyQueueTests
{
    private const int SampleRate = 48000;
    private const int Channels = 2;

    /// <summary>A stereo S16 48 kHz frame carrying the given milliseconds of audio,
    /// with every sample byte set to the marker for FIFO-order assertions.</summary>
    private static WebRtcAudioFrame Frame(int milliseconds, byte marker = 0)
    {
        var bytes = new byte[SampleRate * milliseconds / 1000 * Channels * 2];
        Array.Fill(bytes, marker);
        return new WebRtcAudioFrame(bytes, SampleRate, Channels, TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public async Task FramesAreDequeuedInFifoOrder()
    {
        using var queue = new AudioFrameLatencyQueue(TimeSpan.FromMilliseconds(200));
        Assert.True(queue.TryEnqueue(Frame(20, marker: 1)));
        Assert.True(queue.TryEnqueue(Frame(20, marker: 2)));

        var first = await queue.DequeueAsync();
        var second = await queue.DequeueAsync();

        Assert.Equal(1, first!.Data.Span[0]);
        Assert.Equal(2, second!.Data.Span[0]);
    }

    [Fact]
    public void ExceedingTheLatencyBudgetDropsOldestFrames()
    {
        // 100 ms budget, ten 20 ms frames: the queue must hold the newest five and
        // count the five oldest as local drops.
        using var queue = new AudioFrameLatencyQueue(TimeSpan.FromMilliseconds(100));
        for (byte i = 0; i < 10; i++) queue.TryEnqueue(Frame(20, marker: i));

        var diagnostics = queue.Diagnostics;
        Assert.Equal(10, diagnostics.FramesEnqueued);
        Assert.Equal(5, diagnostics.FramesDropped);
        Assert.Equal(5, diagnostics.Depth);
        Assert.Equal(TimeSpan.FromMilliseconds(100), diagnostics.QueuedDuration);
    }

    [Fact]
    public async Task SurvivorAfterOverflowIsTheOldestRetainedFrame()
    {
        using var queue = new AudioFrameLatencyQueue(TimeSpan.FromMilliseconds(100));
        for (byte i = 0; i < 10; i++) queue.TryEnqueue(Frame(20, marker: i));

        var frame = await queue.DequeueAsync();

        Assert.Equal(5, frame!.Data.Span[0]);
    }

    [Fact]
    public void MaxObservedDurationRecordsThePeakBeforeTrimming()
    {
        using var queue = new AudioFrameLatencyQueue(TimeSpan.FromMilliseconds(100));
        for (byte i = 0; i < 10; i++) queue.TryEnqueue(Frame(20, marker: i));

        // The peak is one frame over budget: 120 ms existed momentarily before the
        // oldest frame was trimmed.
        Assert.Equal(TimeSpan.FromMilliseconds(120), queue.Diagnostics.MaxObservedDuration);
    }

    [Fact]
    public async Task DequeueWaitsUntilAFrameArrives()
    {
        using var queue = new AudioFrameLatencyQueue();
        var pending = queue.DequeueAsync().AsTask();
        await Task.Delay(50);
        Assert.False(pending.IsCompleted);

        queue.TryEnqueue(Frame(20, marker: 9));

        var frame = await pending.WaitAsync(TimeSpan.FromSeconds(5));
        Assert.Equal(9, frame!.Data.Span[0]);
    }

    [Fact]
    public async Task CompleteDrainsThenSignalsEndOfStream()
    {
        using var queue = new AudioFrameLatencyQueue();
        queue.TryEnqueue(Frame(20, marker: 3));
        queue.Complete();

        Assert.False(queue.TryEnqueue(Frame(20)));
        var frame = await queue.DequeueAsync();
        Assert.Equal(3, frame!.Data.Span[0]);
        Assert.Null(await queue.DequeueAsync());
    }

    [Fact]
    public async Task DequeueHonoursCancellation()
    {
        using var queue = new AudioFrameLatencyQueue();
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(50));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            async () => await queue.DequeueAsync(cancellation.Token));
    }

    [Fact]
    public void NonPositiveBudgetIsRejected()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => new AudioFrameLatencyQueue(TimeSpan.Zero));
    }
}
