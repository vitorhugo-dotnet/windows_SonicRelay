using System.Threading.Channels;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.App;

/// <summary>
/// Pumps captured audio frames into the WebRTC publisher. Lives in the App
/// project because it is the only one that references both the Audio and WebRtc
/// assemblies (keeping those two independent of each other). The WASAPI capture
/// callback never blocks: frames are converted to S16 and dropped into a bounded
/// channel that discards the oldest frame under back-pressure, and a background
/// consumer feeds them to <see cref="IWebRtcPublisher.PushAudioFrameAsync"/>.
/// </summary>
public sealed class WebRtcAudioBridge : IAsyncDisposable
{
    private readonly IAudioCaptureService audio;
    private readonly IWebRtcPublisher publisher;
    private readonly Action<string>? onError;
    private readonly Channel<WebRtcAudioFrame> channel;
    private readonly CancellationTokenSource cancellation = new();
    private readonly Task consumer;
    private bool disposed;

    public WebRtcAudioBridge(IAudioCaptureService audio, IWebRtcPublisher publisher, Action<string>? onError = null)
    {
        this.audio = audio ?? throw new ArgumentNullException(nameof(audio));
        this.publisher = publisher ?? throw new ArgumentNullException(nameof(publisher));
        this.onError = onError;
        channel = System.Threading.Channels.Channel.CreateBounded<WebRtcAudioFrame>(
            new BoundedChannelOptions(50) { FullMode = BoundedChannelFullMode.DropOldest, SingleReader = true });
        this.audio.FrameCaptured += OnFrameCaptured;
        consumer = Task.Run(ConsumeAsync);
    }

    private void OnFrameCaptured(AudioFrame frame)
    {
        if (disposed) return;
        try
        {
            var sourceFormat = frame.Format == AudioSampleFormat.IeeeFloat32
                ? WebRtcSourceSampleFormat.IeeeFloat32
                : WebRtcSourceSampleFormat.Pcm16;
            var samples = PcmAudioConverter.ToS16(frame.Data.Span, sourceFormat);
            if (samples.Length == 0) return;
            var bytes = System.Runtime.InteropServices.MemoryMarshal.AsBytes(samples.AsSpan());
            var webRtcFrame = new WebRtcAudioFrame(bytes, frame.SampleRate, frame.ChannelCount, frame.Timestamp);
            channel.Writer.TryWrite(webRtcFrame);
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
            await foreach (var frame in channel.Reader.ReadAllAsync(cancellation.Token).ConfigureAwait(false))
            {
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
        channel.Writer.TryComplete();
        await cancellation.CancelAsync().ConfigureAwait(false);
        try { await consumer.ConfigureAwait(false); }
        catch (OperationCanceledException) { }
        cancellation.Dispose();
    }
}
