using System.Diagnostics;

namespace SonicRelay.Windows.WebRtc;

/// <summary>
/// Paces encoded audio packets onto the wire: one packet per frame deadline on a
/// monotonic (<see cref="Stopwatch"/>) schedule. WASAPI capture callbacks routinely
/// yield several 10/20/40 ms frames at once; without pacing those leave as an RTP
/// burst that the receiver's jitter buffer must absorb or drop. Deadlines are
/// absolute — each one advances by exactly one frame from the previous, so
/// <see cref="Task.Delay(TimeSpan)"/> scheduling error never accumulates as drift.
/// The backlog is bounded by a latency budget: when the schedule falls behind, the
/// oldest packets are discarded and counted instead of growing latency.
/// <see cref="Enqueue"/> never blocks, so it is safe on the capture path.
/// </summary>
public sealed class RtpPacketPacer : IAsyncDisposable
{
    private readonly TimeSpan frameDuration;
    // Stopwatch ticks per frame; the schedule lives on the Stopwatch clock.
    private readonly long frameTimestampTicks;
    private readonly int capacity;
    private readonly Action<byte[]> send;
    private readonly Queue<byte[]> queue = new();
    private readonly object sync = new();
    private readonly SemaphoreSlim signal = new(0);
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task pump;
    private long packetsSent;
    private long packetsDropped;
    private long sendFailures;
    private bool disposed;

    public RtpPacketPacer(TimeSpan frameDuration, TimeSpan latencyBudget, Action<byte[]> send)
    {
        if (frameDuration <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(frameDuration));
        if (latencyBudget < frameDuration)
            throw new ArgumentOutOfRangeException(
                nameof(latencyBudget), "The latency budget must hold at least one frame.");
        this.frameDuration = frameDuration;
        this.send = send ?? throw new ArgumentNullException(nameof(send));
        frameTimestampTicks = (long)(frameDuration.TotalSeconds * Stopwatch.Frequency);
        capacity = (int)(latencyBudget.Ticks / frameDuration.Ticks);
        pump = Task.Run(PumpAsync);
    }

    /// <summary>Packets handed to the send callback.</summary>
    public long PacketsSent => Interlocked.Read(ref packetsSent);

    /// <summary>Stale packets discarded because the backlog exceeded the latency budget.</summary>
    public long PacketsDropped => Interlocked.Read(ref packetsDropped);

    /// <summary>Packets whose send callback threw (e.g. transport already closed).</summary>
    public long SendFailures => Interlocked.Read(ref sendFailures);

    /// <summary>Packets currently queued behind the pacing schedule.</summary>
    public int Backlog
    {
        get { lock (sync) return queue.Count; }
    }

    /// <summary>The backlog expressed as audio time.</summary>
    public TimeSpan BacklogDuration => frameDuration * Backlog;

    /// <summary>
    /// Queues one encoded packet for paced sending, taking ownership of the array.
    /// Never blocks; over the latency budget the oldest queued packets are dropped.
    /// </summary>
    public void Enqueue(byte[] packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.Length == 0) return;
        lock (sync)
        {
            if (disposed) return;
            queue.Enqueue(packet);
            while (queue.Count > capacity)
            {
                queue.Dequeue();
                Interlocked.Increment(ref packetsDropped);
            }
            signal.Release();
        }
    }

    /// <summary>
    /// Discards any queued packets without counting them as drops. Used when the
    /// stream is gated (transport not connected), where buffering would only replay
    /// stale audio later.
    /// </summary>
    public void Clear()
    {
        lock (sync) queue.Clear();
    }

    private async Task PumpAsync()
    {
        var token = cancellation.Token;
        long nextDeadline = 0;
        var anchored = false;
        try
        {
            while (true)
            {
                await signal.WaitAsync(token).ConfigureAwait(false);
                byte[]? packet = null;
                lock (sync)
                {
                    if (queue.Count > 0) packet = queue.Dequeue();
                }
                // A missing packet means this permit's packet was dropped under
                // back-pressure or cleared; the schedule must not advance for it.
                if (packet is null) continue;

                var now = Stopwatch.GetTimestamp();
                if (!anchored || now - nextDeadline > frameTimestampTicks)
                {
                    // Stream start, or the source went idle past a full frame:
                    // re-anchor and send immediately rather than replaying a stale
                    // schedule or letting deadlines pile up in the past.
                    nextDeadline = now;
                    anchored = true;
                }

                var wait = nextDeadline - now;
                if (wait > 0)
                {
                    await Task.Delay(TimeSpan.FromSeconds(wait / (double)Stopwatch.Frequency), token)
                        .ConfigureAwait(false);
                }

                try
                {
                    send(packet);
                    Interlocked.Increment(ref packetsSent);
                }
                catch (Exception)
                {
                    // The transport may close mid-stream; sending must keep pacing
                    // for the frames that follow instead of tearing down the pump.
                    Interlocked.Increment(ref sendFailures);
                }
                nextDeadline += frameTimestampTicks;
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        lock (sync)
        {
            if (disposed) return;
            disposed = true;
            queue.Clear();
        }
        await cancellation.CancelAsync().ConfigureAwait(false);
        try
        {
            await pump.ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
        }
        cancellation.Dispose();
        signal.Dispose();
    }
}
