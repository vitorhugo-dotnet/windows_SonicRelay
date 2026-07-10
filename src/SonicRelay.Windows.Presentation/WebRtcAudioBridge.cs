using SonicRelay.Windows.Audio;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.Presentation;

/// <summary>Capture-to-WebRTC pump counters (issue #31).</summary>
public sealed record AudioBridgeDiagnostics(long FramesCaptured, AudioQueueDiagnostics Queue);

/// <summary>
/// Pumps captured audio frames into the WebRTC publisher. Lives in the shared
/// Presentation project (extracted from the WinUI App for issue #32) so any desktop
/// shell reuses the same pump; it depends only on the capture and WebRTC
/// abstractions, keeping those two independent of each other. The platform capture
/// callback never blocks: frames are converted to S16 and dropped into an
/// <see cref="AudioFrameLatencyQueue"/> bounded by an audio-duration latency
/// budget (default 150 ms) that discards the oldest frames under back-pressure,
/// and a background consumer feeds them to
/// <see cref="IWebRtcPublisher.PushAudioFrameAsync"/>.
/// </summary>
public sealed class WebRtcAudioBridge : IAsyncDisposable
{
    private readonly IAudioCaptureService audio;
    private readonly IWebRtcPublisher publisher;
    private readonly Action<string>? onError;
    private readonly AudioFrameLatencyQueue queue;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task consumer;
    private long framesCaptured;
    private bool disposed;

    public WebRtcAudioBridge(
        IAudioCaptureService audio,
        IWebRtcPublisher publisher,
        Action<string>? onError = null,
        TimeSpan? latencyBudget = null)
    {
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.onError = onError;
        queue = new AudioFrameLatencyQueue(latencyBudget);
        this.audio.FrameCaptured += OnFrameCaptured;
        consumer = Task.Run(ConsumeAsync);
    }

    /// <summary>Local capture/queue/drop counters for the packet-loss diagnostics.</summary>
    public AudioBridgeDiagnostics Diagnostics =>
        new(Interlocked.Read(ref framesCaptured), queue.Diagnostics);

    private void OnFrameCaptured(AudioFrame frame)
    {
        if (disposed) return;
        try
        {
            Interlocked.Increment(ref framesCaptured);
            var sourceFormat = frame.Format == AudioSampleFormat.IeeeFloat32
                ? WebRtcSourceSampleFormat.IeeeFloat32
                : WebRtcSourceSampleFormat.Pcm16;
            var samples = PcmAudioConverter.ToS16(frame.Data.Span, sourceFormat);
            if (samples.Length == 0) return;
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan());
            var webRtcFrame = new WebRtcAudioFrame(bytes, frame.SampleRate, frame.ChannelCount, frame.Timestamp);
            queue.TryEnqueue(webRtcFrame);
        }
        catch (Exception exception)
        {
            onError?.Invoke(exception.Message);
        }
    }

    private async Task ConsumeAsync()
    {
        try
        {
            while (true)
            {
                var frame = await queue.DequeueAsync(cancellation.Token).ConfigureAwait(false);
                if (frame is null) return;
                try
                {
                    await publisher.PushAudioFrameAsync(frame, cancellation.Token).ConfigureAwait(false);
                }
                catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
                {
                    return;
                }
                catch (Exception exception)
                {
                    // A single failed push must not stop the pump; surface it and
                    // keep streaming for the remaining/next viewers.
                    onError?.Invoke(exception.Message);
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (disposed) return;
        disposed = true;
        audio.FrameCaptured -= OnFrameCaptured;
        queue.Complete();
        await cancellation.CancelAsync().ConfigureAwait(false);
        try { await consumer.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        cancellation.Dispose();
        queue.Dispose();
    }
}
