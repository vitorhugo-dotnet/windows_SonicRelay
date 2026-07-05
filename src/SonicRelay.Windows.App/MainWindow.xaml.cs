using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SonicRelay.Windows.App.Pages;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App;

public sealed partial class MainWindow : Window
{
    private PublisherWorkflow? workflow;
    public MainWindow()
    {
        InitializeComponent();
        ConfigureBackdrop();
        ShellNavigation.SelectedItem = ShellNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
        App.CurrentApp.RuntimeChanged += OnRuntimeChanged;
        Closed += OnClosed;
        OnRuntimeChanged(App.CurrentApp.Runtime);
    }

    private void OnRuntimeChanged(PublisherRuntime? runtime)
    {
        if (workflow is not null) workflow.StateChanged -= OnPublisherStateChanged;
        workflow = runtime?.Workflow;
        if (workflow is not null) workflow.StateChanged += OnPublisherStateChanged;
        Render(workflow?.State);
    }

    private void OnPublisherStateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));

    private void Render(PublisherSnapshot? state)
    {
        GlobalStatusText.Text = state is null ? "Backend not configured"
            : state.IsAuthenticated ? $"Signed in · Signaling {state.SignalingState}" : "Backend configured · Sign in required";
        LatestLogText.Text = state is { ActivityLog.Count: > 0 }
            ? state.ActivityLog[^1]
            : "Activity log · No events yet";
    }

    private async void OnClosed(object sender, WindowEventArgs args)
    {
        App.CurrentApp.RuntimeChanged -= OnRuntimeChanged;
        if (workflow is not null) workflow.StateChanged -= OnPublisherStateChanged;
        await App.CurrentApp.DisposeRuntimeAsync();
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
