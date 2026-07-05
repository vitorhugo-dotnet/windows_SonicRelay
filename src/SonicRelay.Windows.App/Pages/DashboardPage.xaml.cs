using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Presentation;
using System.Globalization;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class DashboardPage : Page
{
    private PublisherWorkflow? workflow;
    public DashboardPage() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged += RuntimeChanged; Attach(App.CurrentApp.Runtime); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged -= RuntimeChanged; Attach(null); }
    private void RuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));
    private void Attach(PublisherRuntime? runtime) { if (workflow is not null) workflow.StateChanged -= StateChanged; workflow = runtime?.Workflow; if (workflow is not null) workflow.StateChanged += StateChanged; Render(workflow?.State); }
    private void StateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));
    private void Render(PublisherSnapshot? state)
    {
        AuthText.Text = state?.IsAuthenticated == true ? state.UserDisplayName ?? "Signed in" : "Not signed in";
        DeviceText.Text = state?.DeviceName ?? "No device";
        SessionText.Text = state?.SessionCode ?? "—";
        ViewerText.Text = (state?.ViewerCount ?? 0).ToString(CultureInfo.CurrentCulture);
        AudioText.Text = state?.AudioState.ToString() ?? "Stopped";
        SignalingText.Text = state?.SignalingState.ToString() ?? "Disconnected";
    }
}
