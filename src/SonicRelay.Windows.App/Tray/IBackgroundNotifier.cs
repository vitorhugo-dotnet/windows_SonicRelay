namespace SonicRelay.Windows.App.Tray;

/// <summary>
/// Raises user-visible background notifications (viewer connected, stream
/// started/stopped, minimized-to-tray, …). Implementations must honour the
/// "show notifications" setting so normal reconnect churn never spams the user.
/// </summary>
public interface IBackgroundNotifier
{
    void Notify(string title, string message);
}

/// <summary>
/// Notifier backed by the tray icon's balloon/toast. Using the tray balloon keeps
/// the app fully unpackaged (no AppNotificationManager COM registration) while
/// still surfacing native Windows notifications. Gated by the notifications setting.
/// </summary>
public sealed class TrayBalloonNotifier(ITrayIconService tray, Func<bool> showNotifications) : IBackgroundNotifier
{
    public void Notify(string title, string message)
    {
        if (!showNotifications()) return;
        tray.ShowBalloon(title, message);
    }
}
