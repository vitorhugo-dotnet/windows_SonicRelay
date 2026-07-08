namespace SonicRelay.Windows.App.Tray;

/// <summary>
/// Controls the main window's background lifetime: hide to tray, restore/focus from
/// tray, and an explicit quit that disposes the publisher runtime and exits the
/// process. Keeps window/AppWindow interop out of the tray and streaming layers.
/// </summary>
public interface IAppLifetimeService
{
    /// <summary>Hides the main window; the app keeps running in the tray.</summary>
    void HideToTray();

    /// <summary>Restores and focuses the main window from the tray.</summary>
    void ShowFromTray();

    /// <summary>
    /// Explicit quit: dispose audio → WebRTC → signaling, remove the tray icon, and
    /// exit the process. Safe to call once; further calls are ignored.
    /// </summary>
    Task QuitAsync();
}
