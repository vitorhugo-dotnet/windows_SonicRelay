using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;

namespace SonicRelay.Windows.App.Tray;

/// <summary>
/// <see cref="IAppLifetimeService"/> over a WinUI <see cref="Window"/>/<see cref="AppWindow"/>.
/// Hide/show use the AppWindow so the window can vanish from the taskbar while the
/// process (signaling, WebRTC, audio) keeps running. Quit runs a caller-supplied
/// teardown (dispose the runtime + remove the tray icon) then exits the app.
/// </summary>
public sealed class AppLifetimeService(Window window, Func<Task> teardownAsync) : IAppLifetimeService
{
    private readonly Window window = window;
    private readonly Func<Task> teardownAsync = teardownAsync;
    private bool quitting;

    public void HideToTray() => window.AppWindow.Hide();

    public void ShowFromTray()
    {
        window.AppWindow.Show();
        if (window.AppWindow.Presenter is OverlappedPresenter presenter)
        {
            presenter.Restore();
            presenter.IsAlwaysOnTop = false;
        }

        // Show() alone may leave the window behind others; Activate brings it forward.
        window.Activate();
    }

    public async Task QuitAsync()
    {
        if (quitting) return;
        quitting = true;
        try
        {
            await teardownAsync();
        }
        finally
        {
            Application.Current.Exit();
        }
    }
}
