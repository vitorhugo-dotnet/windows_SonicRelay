using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using SonicRelay.Windows.Core.Configuration;
using SonicRelay.Windows.Presentation;

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
        var tray = App.CurrentApp.TrayPreferences;
        var appearance = App.CurrentApp.AppearancePreferences;
        suppressToggle = true;
        RelayToggle.IsOn = runtime?.RelayPreference.ForceRelay ?? false;
        KeepInTrayToggle.IsOn = tray.KeepRunningInTray;
        StartMinimizedToggle.IsOn = tray.StartMinimized;
        NotificationsToggle.IsOn = tray.ShowNotifications;
        SelectByTag(ThemeCombo, appearance.Theme.ToString());
        SelectByTag(BackdropCombo, appearance.Backdrop.ToString());
        OpacitySlider.Value = appearance.TintOpacity * 100;
        suppressToggle = false;
    }

    private static void SelectByTag(ComboBox combo, string tag)
    {
        foreach (var item in combo.Items)
        {
            if (item is ComboBoxItem { Tag: string value } candidate && value == tag)
            {
                combo.SelectedItem = candidate;
                return;
            }
        }
    }

    private async void AppearanceChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressToggle) return;
        await PersistAppearanceAsync();
    }

    private async void OpacityChanged(object sender, RangeBaseValueChangedEventArgs e)
    {
        if (suppressToggle) return;
        await PersistAppearanceAsync();
    }

    private async Task PersistAppearanceAsync()
    {
        var theme = Enum.Parse<AppTheme>((string)((ComboBoxItem)ThemeCombo.SelectedItem).Tag);
        var backdrop = Enum.Parse<AppBackdrop>((string)((ComboBoxItem)BackdropCombo.SelectedItem).Tag);
        await App.CurrentApp.AppearancePreferences.UpdateAsync(theme, backdrop, OpacitySlider.Value / 100.0);
        App.CurrentApp.ApplyAppearance();
    }

    private async void RelayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (suppressToggle) return;
        var runtime = App.CurrentApp.Runtime;
        if (runtime is null) return;
        await runtime.RelayPreference.SetForceRelayAsync(RelayToggle.IsOn);
    }

    private async void TrayToggle_Toggled(object sender, RoutedEventArgs e)
    {
        if (suppressToggle) return;
        await App.CurrentApp.TrayPreferences.UpdateAsync(
            KeepInTrayToggle.IsOn, StartMinimizedToggle.IsOn, NotificationsToggle.IsOn);
    }
}
