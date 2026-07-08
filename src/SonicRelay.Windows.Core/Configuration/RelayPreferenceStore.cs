using System.Text.Json;

namespace SonicRelay.Windows.Core.Configuration;

/// <summary>
/// Persists the user's "force relay (TURN only)" ICE preference. Kept in a small
/// file next to the publisher configuration so a settings toggle can update it at
/// runtime without rewriting the whole app configuration. The current value is
/// cached in memory; the WebRTC factory reads it when creating each connection.
/// </summary>
public sealed class RelayPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(UserConfigurationLoader.DefaultDirectory, "preferences.json");

    private readonly string _path;

    public RelayPreferenceStore(string? path = null)
    {
        _path = path ?? DefaultPath;
        ForceRelay = Load();
    }

    /// <summary>When true, ICE is restricted to relay (TURN) candidates.</summary>
    public bool ForceRelay { get; private set; }

    public async Task SetForceRelayAsync(bool value, CancellationToken cancellationToken = default)
    {
        ForceRelay = value;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(
            _path,
            JsonSerializer.Serialize(new PreferencesDocument(value), JsonOptions),
            cancellationToken);
    }

    private bool Load()
    {
        try
        {
            if (!File.Exists(_path)) return false;
            var document = JsonSerializer.Deserialize<PreferencesDocument>(File.ReadAllText(_path), JsonOptions);
            return document?.ForceRelay ?? false;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A missing/corrupt preferences file must never block startup; default to direct.
            return false;
        }
    }

    private sealed record PreferencesDocument(bool ForceRelay);
}
