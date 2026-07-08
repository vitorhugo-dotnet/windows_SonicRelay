using System.Text.Json;
using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.Core.Audio;

/// <summary>
/// Persists the selected <see cref="AudioQualityProfile"/> (including custom
/// values) in a small JSON file next to the other user preferences. The current
/// value is cached in memory; the WebRTC factory reads it when creating each
/// connection, so a change applies to the next stream. Mirrors
/// <see cref="RelayPreferenceStore"/>.
/// </summary>
public sealed class AudioQualityStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(UserConfigurationLoader.DefaultDirectory, "audio-quality.json");

    private readonly string _path;

    public AudioQualityStore(string? path = null)
    {
        _path = path ?? DefaultPath;
        CurrentProfile = Load();
    }

    /// <summary>The active profile. Never null; defaults to <see cref="AudioQualityProfile.Default"/>.</summary>
    public AudioQualityProfile CurrentProfile { get; private set; }

    public async Task SetProfileAsync(AudioQualityProfile profile, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);
        profile.Validate();
        CurrentProfile = profile;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(
            _path,
            JsonSerializer.Serialize(profile, JsonOptions),
            cancellationToken);
    }

    private AudioQualityProfile Load()
    {
        try
        {
            if (!File.Exists(_path)) return AudioQualityProfile.Default;
            var profile = JsonSerializer.Deserialize<AudioQualityProfile>(File.ReadAllText(_path), JsonOptions);
            if (profile is null) return AudioQualityProfile.Default;
            profile.Validate();
            return profile;
        }
        catch (Exception exception) when (
            exception is IOException or JsonException or UnauthorizedAccessException or ArgumentException)
        {
            // A missing/corrupt/invalid profile file must never block startup.
            return AudioQualityProfile.Default;
        }
    }
}
