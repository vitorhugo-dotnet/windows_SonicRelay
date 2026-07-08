using System.Text.Json;

namespace SonicRelay.Windows.Core.Configuration;

/// <summary>
/// Persists which Windows render (output) endpoint the publisher captures. A null
/// device id means "follow the system default" (the initial behaviour). The
/// friendly name is stored only for display; the id is authoritative. Kept in a
/// small user-scoped JSON file, mirroring <see cref="RelayPreferenceStore"/>.
/// </summary>
public sealed class AudioOutputPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions =
        new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(UserConfigurationLoader.DefaultDirectory, "audio-output.json");

    private readonly string _path;

    public AudioOutputPreferenceStore(string? path = null)
    {
        _path = path ?? DefaultPath;
        var document = Load();
        SelectedDeviceId = document?.DeviceId;
        SelectedDeviceName = document?.DeviceName;
    }

    /// <summary>The selected render device id, or null for the system default.</summary>
    public string? SelectedDeviceId { get; private set; }

    /// <summary>Friendly name of the selected device, for display only.</summary>
    public string? SelectedDeviceName { get; private set; }

    public async Task SetSelectedDeviceAsync(string? deviceId, string? deviceName, CancellationToken cancellationToken = default)
    {
        SelectedDeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId;
        SelectedDeviceName = SelectedDeviceId is null ? null : deviceName;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(
            _path,
            JsonSerializer.Serialize(new AudioOutputDocument(SelectedDeviceId, SelectedDeviceName), JsonOptions),
            cancellationToken);
    }

    private AudioOutputDocument? Load()
    {
        try
        {
            if (!File.Exists(_path)) return null;
            return JsonSerializer.Deserialize<AudioOutputDocument>(File.ReadAllText(_path), JsonOptions);
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A missing/corrupt preferences file must never block startup; use the default.
            return null;
        }
    }

    private sealed record AudioOutputDocument(string? DeviceId, string? DeviceName);
}
