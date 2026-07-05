using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Presentation;
using System.Globalization;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class SessionPage : Page
{
    private PublisherWorkflow? workflow;
    public SessionPage() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged += RuntimeChanged; Attach(App.CurrentApp.Runtime); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged -= RuntimeChanged; Attach(null); }
    private void RuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));
    private void Attach(PublisherRuntime? runtime) { if (workflow is not null) workflow.StateChanged -= StateChanged; workflow = runtime?.Workflow; if (workflow is not null) workflow.StateChanged += StateChanged; Render(workflow?.State); }
    private async void Create_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.CreateSessionAsync(); }
    private async void Refresh_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.RefreshViewerCountAsync(); }
    private async void End_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.EndSessionAsync(); }
    private void StateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));
    private void Render(PublisherSnapshot? state)
    {
        CodeText.Text = state?.SessionCode ?? "—";
        SignalingText.Text = state?.SignalingState.ToString() ?? "Disconnected";
        ViewerText.Text = (state?.ViewerCount ?? 0).ToString(CultureInfo.CurrentCulture);
        CreateButton.IsEnabled = state?.CanCreateSession == true;
        RefreshButton.IsEnabled = state?.SessionId is not null && state.IsBusy == false;
        EndButton.IsEnabled = state?.CanEndSession == true;
        ErrorBar.Message = state?.ErrorMessage ?? string.Empty;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(state?.ErrorMessage);
    }
}
