using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using SonicRelay.Windows.App.Pages;

namespace SonicRelay.Windows.App;

public sealed partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        ConfigureBackdrop();
        ShellNavigation.SelectedItem = ShellNavigation.MenuItems[0];
        ContentFrame.Navigate(typeof(DashboardPage));
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
