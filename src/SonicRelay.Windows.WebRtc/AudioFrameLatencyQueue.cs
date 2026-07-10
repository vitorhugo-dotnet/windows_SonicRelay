namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Counters for a capture-side frame queue. Durations are audio time (how much
/// playable audio is buffered), not wall-clock time.
/// </summary>
public sealed record AudioQueueDiagnostics(
    long FramesEnqueued,
    long FramesDropped,
    int Depth,
    TimeSpan QueuedDuration,
    TimeSpan MaxObservedDuration);

/// <summary>
/// Bounded FIFO of captured audio frames whose limit is an audio-duration latency
/// budget instead of an arbitrary frame count. Producers never block: when the
/// consumer falls behind, the oldest frames are dropped and counted, keeping the
/// WASAPI capture callback non-blocking and end-to-end latency bounded. Media is
/// only ever held in memory — nothing is persisted.
/// </summary>
public sealed class AudioFrameLatencyQueue : IDisposable
{
    /// <summary>Default budget, the middle of the 100–200 ms range from issue #31.</summary>
    public static readonly TimeSpan DefaultLatencyBudget = TimeSpan.FromMilliseconds(150);

    private readonly TimeSpan latencyBudget;
    private readonly Queue<WebRtcAudioFrame> queue = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly object sync = new();
    private TimeSpan queuedDuration;
    private TimeSpan maxObservedDuration;
    private long framesEnqueued;
    private long framesDropped;
    private bool completed;

    public AudioFrameLatencyQueue(TimeSpan? latencyBudget = null)
    {
        var budget = latencyBudget ?? DefaultLatencyBudget;
        if (budget <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(latencyBudget));
        this.latencyBudget = budget;
    }

    public AudioQueueDiagnostics Diagnostics
    {
        get
        {
            lock (sync)
            {
                return new AudioQueueDiagnostics(
                    framesEnqueued, framesDropped, queue.Count, queuedDuration, maxObservedDuration);
            }
        }
    }

    /// <summary>
    /// Enqueues a captured frame, dropping the oldest queued frames once the budget
    /// is exceeded. Returns false after <see cref="Complete"/>. Never blocks.
    /// </summary>
    public bool TryEnqueue(WebRtcAudioFrame frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        lock (sync)
        {
            if (completed) return false;
            queue.Enqueue(frame);
            queuedDuration += DurationOf(frame);
            framesEnqueued++;
            if (queuedDuration > maxObservedDuration) maxObservedDuration = queuedDuration;
            while (queuedDuration > latencyBudget && queue.Count > 1)
            {
                var stale = queue.Dequeue();
                queuedDuration -= DurationOf(stale);
                framesDropped++;
            }
            signal.Release();
        }
        return true;
    }

    /// <summary>
    /// Waits for the next frame; returns null once the queue is completed and
    /// drained. Intended for a single consumer.
    /// </summary>
    public async ValueTask<WebRtcAudioFrame?> DequeueAsync(CancellationToken cancellationToken = default)
    {
        while (true)
        {
            await signal.WaitAsync(cancellationToken).ConfigureAwait(false);
            lock (sync)
            {
                if (queue.Count > 0)
                {
                    var frame = queue.Dequeue();
                    queuedDuration -= DurationOf(frame);
                    return frame;
                }
                if (completed) return null;
            }
            // Stale permit left behind by a dropped frame; wait for the next one.
        }
    }

    /// <summary>Marks the queue closed for producers and wakes a pending consumer.</summary>
    public void Complete()
    {
        lock (sync)
        {
            if (completed) return;
            completed = true;
            signal.Release();
        }
    }

    /// <summary>How much audio time one frame carries (S16 interleaved payload).</summary>
    private static TimeSpan DurationOf(WebRtcAudioFrame frame) =>
        TimeSpan.FromSeconds(
            frame.Data.Length / 2.0 / frame.ChannelCount / frame.SampleRate);

    /// <summary>Call only after <see cref="Complete"/> and once the consumer has stopped.</summary>
    public void Dispose() => signal.Dispose();
}
