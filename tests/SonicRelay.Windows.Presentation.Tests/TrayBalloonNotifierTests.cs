using SonicRelay.Windows.Presentation.Platform;

namespace SonicRelay.Windows.Presentation.Tests;

public sealed class TrayBalloonNotifierTests
{
    private sealed class FakeTray : ISystemTrayService
    {
        public List<(string Title, string Message)> Balloons { get; } = [];
        public event Action<TrayCommand>? CommandInvoked { add { } remove { } }
        public event Action? Activated { add { } remove { } }
        public void Show() { }
        public void Hide() { }
        public void UpdateMenu(IReadOnlyList<TrayMenuItem> items) { }
        public void UpdateTooltip(string tooltip) { }
        public void ShowBalloon(string title, string message) => Balloons.Add((title, message));
        public void Dispose() { }
    }

    [Fact]
    public void Notifies_through_the_tray_when_notifications_are_enabled()
    {
        var tray = new FakeTray();
        new TrayBalloonNotifier(tray, () => true).Notify("Title", "Body");
        Assert.Equal([("Title", "Body")], tray.Balloons);
    }

    [Fact]
    public void Stays_silent_when_notifications_are_disabled()
    {
        var tray = new FakeTray();
        new TrayBalloonNotifier(tray, () => false).Notify("Title", "Body");
        Assert.Empty(tray.Balloons);
    }
}
