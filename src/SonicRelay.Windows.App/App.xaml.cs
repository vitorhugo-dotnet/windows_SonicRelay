using Microsoft.UI.Xaml;
using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.App;

public partial class App : Application
{
    private Window? _window;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _ = new UserConfigurationLoader().LoadAsync().GetAwaiter().GetResult();
        _window = new MainWindow();
        _window.Activate();
    }
}
