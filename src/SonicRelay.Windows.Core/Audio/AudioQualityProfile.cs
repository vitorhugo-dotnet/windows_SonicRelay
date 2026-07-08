namespace SonicRelay.Windows.Core.Audio;

/// <summary>
/// Estimated network traffic for an audio profile, derived from the Opus target
/// bitrate. Approximate: it accounts for the media payload only, not RTP/UDP/SRTP
/// header overhead, which is negligible for a status readout.
/// </summary>
public sealed record AudioTrafficEstimate(int Kbps, double MegabytesPerMinute, double MegabytesPerHour);

/// <summary>
/// A selectable Opus encode profile for the Windows publisher. WASAPI loopback
/// capture is unchanged; this only controls how audio is encoded before it leaves
/// over WebRTC. The sample rate stays fixed at 48 kHz for Opus/WebRTC compatibility.
/// </summary>
public sealed record AudioQualityProfile(
    string Id,
    string DisplayName,
    int Channels,
    int OpusBitrateKbps,
    int FrameDurationMs,
    int SampleRateHz)
{
    public const int MinBitrateKbps = 16;
    public const int MaxBitrateKbps = 192;
    public const int FixedSampleRateHz = 48000;
    public const string CustomId = "custom";

    private static readonly int[] AllowedFrameDurationsMs = [10, 20, 40];

    public static AudioQualityProfile Voice { get; } =
        new("voice", "Voice / Economy", 1, 32, 20, FixedSampleRateHz);

    public static AudioQualityProfile Balanced { get; } =
        new("balanced", "Balanced", 2, 96, 20, FixedSampleRateHz);

    public static AudioQualityProfile High { get; } =
        new("high", "High quality", 2, 128, 20, FixedSampleRateHz);

    /// <summary>The default when nothing is persisted — preserves the app's original
    /// 128 kbps stereo music quality.</summary>
    public static AudioQualityProfile Default => High;

    public static IReadOnlyList<AudioQualityProfile> Presets { get; } = [Voice, Balanced, High];

    /// <summary>Resolves a built-in preset by id; returns null for custom/unknown ids.</summary>
    public static AudioQualityProfile? FromId(string id) =>
        Presets.FirstOrDefault(preset => string.Equals(preset.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>Builds and validates a user-defined custom profile at the fixed 48 kHz rate.</summary>
    public static AudioQualityProfile Custom(int channels, int bitrateKbps, int frameDurationMs)
    {
        var profile = new AudioQualityProfile(
            CustomId, "Custom", channels, bitrateKbps, frameDurationMs, FixedSampleRateHz);
        profile.Validate();
        return profile;
    }

    /// <summary>Throws <see cref="ArgumentException"/> if any field is out of the supported range.</summary>
    public void Validate()
    {
        if (Channels is < 1 or > 2)
            throw new ArgumentException($"Channels must be 1 or 2, was {Channels}.", nameof(Channels));
        if (OpusBitrateKbps is < MinBitrateKbps or > MaxBitrateKbps)
            throw new ArgumentException(
                $"Opus bitrate must be between {MinBitrateKbps} and {MaxBitrateKbps} kbps, was {OpusBitrateKbps}.",
                nameof(OpusBitrateKbps));
        if (Array.IndexOf(AllowedFrameDurationsMs, FrameDurationMs) < 0)
            throw new ArgumentException(
                $"Frame duration must be 10, 20, or 40 ms, was {FrameDurationMs}.", nameof(FrameDurationMs));
        if (SampleRateHz != FixedSampleRateHz)
            throw new ArgumentException(
                $"Sample rate must be {FixedSampleRateHz} Hz, was {SampleRateHz}.", nameof(SampleRateHz));
    }

    /// <summary>Whether this profile matches one of the built-in presets by id.</summary>
    public bool IsPreset => FromId(Id) is not null;

    public AudioTrafficEstimate EstimateTraffic()
    {
        var bytesPerSecond = OpusBitrateKbps * 1000.0 / 8.0;
        return new AudioTrafficEstimate(
            OpusBitrateKbps,
            bytesPerSecond * 60 / 1_000_000,
            bytesPerSecond * 3600 / 1_000_000);
    }
}
