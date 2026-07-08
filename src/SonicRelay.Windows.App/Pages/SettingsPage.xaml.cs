using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class SettingsPage : Page
{
    // Suppresses the Toggled handler while we set IsOn programmatically from state.
    private bool suppressToggle;

    public SettingsPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.CurrentApp.RuntimeChanged += OnRuntimeChanged;
        Render();
    }

    private void OnUnloaded(object sender, RoutedEventArgs e) =>
        App.CurrentApp.RuntimeChanged -= OnRuntimeChanged;

    private void OnRuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(Render);

    private void Render()
    {
        var runtime = App.CurrentApp.Runtime;
        RelayToggle.IsEnabled = runtime is not null;
        suppressToggle = true;
        RelayToggle.IsOn = runtime?.RelayPreference.ForceRelay ?? false;
        suppressToggle = false;
    }

    private async void RelayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (suppressToggle) return;
        var runtime = App.CurrentApp.Runtime;
        if (runtime is null) return;
        await runtime.RelayPreference.SetForceRelayAsync(RelayToggle.IsOn);
    }
}
