using System.Text.Json;

namespace SonicRelay.Windows.Core.Configuration;

/// <summary>
/// Persists the user's background/tray preferences (issue #26). Mirrors
/// <see cref="RelayPreferenceStore"/>: a small user-scoped JSON file whose current
/// values are cached in memory so the window lifetime and tray code can read them
/// synchronously. A missing or corrupt file must never block startup, so it falls
/// back to safe defaults (keep running in tray on, notifications on).
/// </summary>
public sealed class TrayBackgroundPreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };

    public static string DefaultPath => Path.Combine(UserConfigurationLoader.DefaultDirectory, "tray.json");

    private readonly string _path;

    public TrayBackgroundPreferenceStore(string? path = null)
    {
        _path = path ?? DefaultPath;
        var document = Load();
        KeepRunningInTray = document.KeepRunningInTray;
        StartMinimized = document.StartMinimized;
        ShowNotifications = document.ShowNotifications;
    }

    /// <summary>Keep the app alive in the tray when the main window is closed.</summary>
    public bool KeepRunningInTray { get; private set; }

    /// <summary>Start hidden to the tray instead of showing the window.</summary>
    public bool StartMinimized { get; private set; }

    /// <summary>Show background/tray notifications for viewer and stream events.</summary>
    public bool ShowNotifications { get; private set; }

    public async Task UpdateAsync(
        bool keepRunningInTray,
        bool startMinimized,
        bool showNotifications,
        CancellationToken cancellationToken = default)
    {
        KeepRunningInTray = keepRunningInTray;
        StartMinimized = startMinimized;
        ShowNotifications = showNotifications;
        Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
        await File.WriteAllTextAsync(
            _path,
            JsonSerializer.Serialize(new PreferencesDocument(keepRunningInTray, startMinimized, showNotifications), JsonOptions),
            cancellationToken);
    }

    private PreferencesDocument Load()
    {
        try
        {
            if (!File.Exists(_path)) return PreferencesDocument.Default;
            return JsonSerializer.Deserialize<PreferencesDocument>(File.ReadAllText(_path), JsonOptions)
                ?? PreferencesDocument.Default;
        }
        catch (Exception exception) when (exception is IOException or JsonException or UnauthorizedAccessException)
        {
            // A missing/corrupt tray-preferences file must never block startup.
            return PreferencesDocument.Default;
        }
    }

    private sealed record PreferencesDocument(bool KeepRunningInTray, bool StartMinimized, bool ShowNotifications)
    {
        public static PreferencesDocument Default { get; } = new(KeepRunningInTray: true, StartMinimized: false, ShowNotifications: true);
    }
}
