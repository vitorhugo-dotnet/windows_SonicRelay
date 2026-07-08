namespace SonicRelay.Windows.Audio;

public enum AudioCaptureState { Stopped, Starting, Capturing, Paused, Stopping, Recovering, Faulted }

public enum AudioSampleFormat { Pcm16, IeeeFloat32 }

public enum AudioCaptureError { NoDevice, DeviceLost, UnsupportedFormat, AccessDenied, PlatformFailure }

public sealed record AudioDeviceInfo(
    string Id,
    string Name,
    int SampleRate,
    int ChannelCount,
    AudioSampleFormat Format);

/// <summary>A selectable Windows render (output) endpoint for the source picker.</summary>
public sealed record AudioOutputDevice(string Id, string Name, bool IsDefault);

/// <summary>
/// Enumerates the active Windows render endpoints so the user can choose which
/// output is captured/published. Implementations must never throw; they return an
/// empty list when enumeration is unavailable.
/// </summary>
public interface IAudioOutputDeviceProbe
{
    IReadOnlyList<AudioOutputDevice> GetOutputDevices();
}

/// <summary>No-op probe for non-Windows platforms and tests.</summary>
public sealed class NullOutputDeviceProbe : IAudioOutputDeviceProbe
{
    public IReadOnlyList<AudioOutputDevice> GetOutputDevices() => [];
}

public sealed class AudioFrame
{
    private readonly byte[] _data;

    public AudioFrame(ReadOnlySpan<byte> data, int sampleRate, int channelCount, AudioSampleFormat format, TimeSpan timestamp)
    {
        if (data.IsEmpty) throw new ArgumentException("Audio frame data cannot be empty.", nameof(data));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(sampleRate);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(channelCount);
        ArgumentOutOfRangeException.ThrowIfLessThan(timestamp, TimeSpan.Zero);
        _data = data.ToArray();
        SampleRate = sampleRate;
        ChannelCount = channelCount;
        Format = format;
        Timestamp = timestamp;
    }

    public ReadOnlyMemory<byte> Data => _data;
    public int SampleRate { get; }
    public int ChannelCount { get; }
    public AudioSampleFormat Format { get; }
    public TimeSpan Timestamp { get; }
}

public sealed record AudioLevelSnapshot
{
    public static AudioLevelSnapshot Silence { get; } = new(0, 0);

    public AudioLevelSnapshot(float peak, float rms)
    {
        if (!float.IsFinite(peak) || peak is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(peak));
        if (!float.IsFinite(rms) || rms is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(rms));
        Peak = peak;
        Rms = rms;
    }

    public float Peak { get; }
    public float Rms { get; }
}

public sealed record AudioCaptureFailure(AudioCaptureError Code, string Message);

public sealed record AudioCaptureDiagnostics(
    AudioCaptureState State,
    AudioDeviceInfo? Device,
    AudioCaptureFailure? LastError,
    AudioLevelSnapshot Level,
    long BytesCaptured,
    long FramesCaptured);

public sealed class AudioCaptureException(AudioCaptureError error, string message, Exception? innerException = null)
    : Exception(message, innerException)
{
    public AudioCaptureError Error { get; } = error;
}
