namespace SonicRelay.Windows.Presentation.Platform;

/// <summary>
/// Raises user-visible background notifications (viewer connected, stream
/// started/stopped, minimized-to-tray, …). Implementations must honour the
/// "show notifications" setting so normal reconnect churn never spams the user.
/// </summary>
public interface INotificationService
{
    void Notify(string title, string message);
}

/// <summary>
/// Notifier backed by the tray icon's balloon/toast. Using the tray balloon keeps
/// the app fully unpackaged (no AppNotificationManager COM registration on Windows)
/// while still surfacing native notifications. Gated by the notifications setting.
/// </summary>
public sealed class TrayBalloonNotifier(ISystemTrayService tray, Func<bool> showNotifications) : INotificationService
{
    public void Notify(string title, string message)
    {
        if (!showNotifications()) return;
        tray.ShowBalloon(title, message);
    }
}
