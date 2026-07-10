namespace SonicRelay.Windows.Presentation.Platform;

/// <summary>
/// Owns the platform notification-area (tray) icon and its context menu. The
/// streaming/audio/WebRTC layers never see this interface — it isolates the shell
/// interop (Win32 today, StatusNotifierItem/AppIndicator on Linux later, issue #32)
/// so the rest of the app deals only in <see cref="TrayCommand"/>s.
/// </summary>
public interface ISystemTrayService : IDisposable
{
    /// <summary>Adds the icon to the notification area (idempotent).</summary>
    void Show();

    /// <summary>Removes the icon from the notification area.</summary>
    void Hide();

    /// <summary>Replaces the context-menu contents shown on right-click.</summary>
    void UpdateMenu(IReadOnlyList<TrayMenuItem> items);

    /// <summary>Sets the hover tooltip text.</summary>
    void UpdateTooltip(string tooltip);

    /// <summary>Shows a balloon/toast from the tray icon.</summary>
    void ShowBalloon(string title, string message);

    /// <summary>Raised when the user picks a menu entry.</summary>
    event Action<TrayCommand>? CommandInvoked;

    /// <summary>Raised on double-click (restore/focus the window).</summary>
    event Action? Activated;
}
