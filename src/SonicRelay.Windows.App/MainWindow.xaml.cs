using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SonicRelay.Windows.App.Pages;
using SonicRelay.Windows.App.Tray;
using SonicRelay.Windows.Presentation;
using Windows.ApplicationModel.DataTransfer;

namespace SonicRelay.Windows.App;

public sealed partial class MainWindow : Window
{
    private PublisherWorkflow? workflow;

    // Background/tray coordination (issue #26). The decision core is pure and unit
    // tested; here we only glue it to Win32 tray/lifetime/notification services.
    private readonly TrayApplicationController tray;
    private readonly ITrayIconService trayIcon;
    private readonly IAppLifetimeService lifetime;
    private readonly IBackgroundNotifier notifier;
    private PublisherSnapshot? lastSnapshot;
    private bool suppressMinimizeHandling;
    private bool minimizedNoticeShown;
    private bool quitting;

    public MainWindow()
    {
        InitializeComponent();
        TrySetWindowIcon();
        ConfigureBackdrop();

        tray = new TrayApplicationController(() => App.CurrentApp.TrayPreferences.KeepRunningInTray);
        trayIcon = new Win32TrayIconService(tray.TooltipFor(null));
        lifetime = new AppLifetimeService(this, TeardownAsync);
        notifier = new TrayBalloonNotifier(trayIcon, () => App.CurrentApp.TrayPreferences.ShowNotifications);
        trayIcon.CommandInvoked += OnTrayCommand;
        trayIcon.Activated += () => DispatcherQueue.TryEnqueue(lifetime.ShowFromTray);
        trayIcon.UpdateMenu(tray.BuildMenu(null));
        trayIcon.Show();

        AppWindow.Closing += OnAppWindowClosing;
        AppWindow.Changed += OnAppWindowChanged;

        ShellNavigation.SelectedItem = ShellNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
        App.CurrentApp.RuntimeChanged += OnRuntimeChanged;
        Closed += OnClosed;
        OnRuntimeChanged(App.CurrentApp.Runtime);
    }

    /// <summary>Hide the window at launch for the "start minimized to tray" setting.</summary>
    public void HideToTrayOnStartup() => lifetime.HideToTray();

    // WinUI does not inherit the .exe icon for the title bar / taskbar, so set it
    // explicitly from the bundled multi-size icon. Best-effort: never block startup.
    private void TrySetWindowIcon()
    {
        try
        {
            AppWindow.SetIcon("Assets/app.ico");
        }
        catch
        {
            // Icon is cosmetic; ignore load failures (e.g. missing asset in odd run configs).
        }
    }

    private void OnRuntimeChanged(PublisherRuntime? runtime)
    {
        if (workflow is not null) workflow.StateChanged -= OnPublisherStateChanged;
        workflow = runtime?.Workflow;
        if (workflow is not null) workflow.StateChanged += OnPublisherStateChanged;
        ApplySnapshot(workflow?.State);
    }

    private void OnPublisherStateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => ApplySnapshot(state));

    // Single sink for state changes: refresh the window text, the tray menu/tooltip,
    // and raise any noteworthy background notification.
    private void ApplySnapshot(PublisherSnapshot? state)
    {
        var previous = lastSnapshot;
        lastSnapshot = state;
        Render(state);

        trayIcon.UpdateMenu(tray.BuildMenu(state));
        trayIcon.UpdateTooltip(tray.TooltipFor(state));

        // Only notify while hidden — a visible window already shows these transitions.
        if (!IsWindowVisible() && tray.DiffNotice(previous, state) is { } notice)
            notifier.Notify(notice.Title, notice.Message);
    }

    private void Render(PublisherSnapshot? state)
    {
        GlobalStatusText.Text = state is null ? "Backend not configured"
            : state.IsAuthenticated ? $"Signed in · Signaling {state.SignalingState}" : "Backend configured · Sign in required";
        LatestLogText.Text = state is { ActivityLog.Count: > 0 }
            ? state.ActivityLog[^1]
            : "Activity log · No events yet";
    }

    // Intercept the native close: the window never truly closes on the [X]. Instead we
    // hide to the tray (keeping signaling/WebRTC/audio alive) or run an explicit quit.
    private void OnAppWindowClosing(AppWindow sender, AppWindowClosingEventArgs args)
    {
        if (quitting) return; // let the programmatic close during quit proceed
        args.Cancel = true;
        if (tray.DecideOnClose(lastSnapshot) == TrayCloseDecision.Hide)
        {
            HideToTrayWithNotice();
        }
        else
        {
            _ = lifetime.QuitAsync();
        }
    }

    private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
    {
        if (suppressMinimizeHandling) return;
        if (sender.Presenter is OverlappedPresenter { State: OverlappedPresenterState.Minimized }
            && tray.DecideOnMinimize() == TrayCloseDecision.Hide)
        {
            HideToTrayWithNotice();
        }
    }

    private void HideToTrayWithNotice()
    {
        suppressMinimizeHandling = true;
        lifetime.HideToTray();
        suppressMinimizeHandling = false;

        if (!minimizedNoticeShown)
        {
            minimizedNoticeShown = true;
            notifier.Notify("SonicRelay", "Still running in the tray. Right-click the tray icon to control the stream.");
        }
    }

    private void OnTrayCommand(TrayCommand command) => DispatcherQueue.TryEnqueue(() =>
    {
        var state = lastSnapshot;
        switch (command)
        {
            case TrayCommand.Open:
                lifetime.ShowFromTray();
                break;
            case TrayCommand.StartStream:
                if (workflow is not null) _ = workflow.StartAudioAsync();
                break;
            case TrayCommand.StopStream:
                if (workflow is not null) _ = workflow.StopAudioAsync();
                break;
            case TrayCommand.ReconnectSignaling:
                if (workflow is not null) _ = workflow.ReconnectSignalingAsync();
                break;
            case TrayCommand.CopySessionCode:
                CopySessionCode(state?.SessionCode);
                break;
            case TrayCommand.Quit:
                _ = lifetime.QuitAsync();
                break;
            case TrayCommand.Status:
            default:
                break;
        }
    });

    private static void CopySessionCode(string? code)
    {
        if (string.IsNullOrWhiteSpace(code)) return;
        var package = new DataPackage();
        package.SetText(code);
        Clipboard.SetContent(package);
    }

    private bool IsWindowVisible() => AppWindow.IsVisible
        && AppWindow.Presenter is not OverlappedPresenter { State: OverlappedPresenterState.Minimized };

    // Explicit-quit teardown: dispose audio → WebRTC → signaling and remove the tray
    // icon so it never lingers after exit. Called by AppLifetimeService.QuitAsync.
    private async Task TeardownAsync()
    {
        quitting = true;
        App.CurrentApp.RuntimeChanged -= OnRuntimeChanged;
        if (workflow is not null) workflow.StateChanged -= OnPublisherStateChanged;
        trayIcon.Dispose();
        await App.CurrentApp.DisposeRuntimeAsync();
    }

    private void OnClosed(object sender, WindowEventArgs args)
    {
        // Reached only on a real close (during explicit quit); teardown already ran.
        trayIcon.Dispose();
    }

    private static SolidColorBrush SolidFallbackBrush =>
        (SolidColorBrush)Application.Current.Resources["AppBackgroundBrush"];

    private void ConfigureBackdrop()
    {
        RootGrid.Background = SolidFallbackBrush;
        try
        {
            SystemBackdrop = new MicaBackdrop { Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base };
        }
        catch
        {
            SystemBackdrop = null;
        }
    }

    private void OnNavigationSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
    {
        if (args.SelectedItemContainer?.Tag is not string tag) return;
        Type? page = tag switch
        {
            "Dashboard" => typeof(DashboardPage), "Connection" => typeof(ConnectionPage),
            "Session" => typeof(SessionPage), "Audio" => typeof(AudioPage),
            "Diagnostics" => typeof(DiagnosticsPage), "Settings" => typeof(SettingsPage), _ => null
        };
        if (page is not null && ContentFrame.CurrentSourcePageType != page) ContentFrame.Navigate(page);
    }
}
