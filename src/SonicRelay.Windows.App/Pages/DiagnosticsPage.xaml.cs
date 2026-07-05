using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class DiagnosticsPage : Page
{
    private PublisherWorkflow? workflow;
    public DiagnosticsPage() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged += RuntimeChanged; Attach(App.CurrentApp.Runtime); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged -= RuntimeChanged; Attach(null); }
    private void RuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));
    private void Attach(PublisherRuntime? runtime) { if (workflow is not null) workflow.StateChanged -= StateChanged; workflow = runtime?.Workflow; if (workflow is not null) workflow.StateChanged += StateChanged; Render(workflow?.State); }
    private void StateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));
    private void Render(PublisherSnapshot? state) => LogList.ItemsSource = state?.ActivityLog.Reverse().ToArray() ?? [];
}
