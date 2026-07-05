using Microsoft.UI.Xaml;
using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.App;

public partial class App : Application
{
    private Window? _window;
    private PublisherRuntime? runtime;

    public static App CurrentApp => (App)Current;
    public PublisherRuntime? Runtime => runtime;
    public event Action<PublisherRuntime?>? RuntimeChanged;

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        _ = LoadConfiguredRuntimeAsync();
    }

    public async Task ConfigureBackendAsync(Uri backendBaseUrl)
    {
        var replacement = PublisherRuntime.Create(backendBaseUrl);
        var previous = runtime;
        runtime = replacement;
        RuntimeChanged?.Invoke(runtime);
        if (previous is not null) await previous.DisposeAsync();
    }

    public async Task DisposeRuntimeAsync()
    {
        var previous = runtime;
        runtime = null;
        RuntimeChanged?.Invoke(null);
        if (previous is not null) await previous.DisposeAsync();
    }

    private async Task LoadConfiguredRuntimeAsync()
    {
        try
        {
            var configuration = await new UserConfigurationLoader().LoadAsync();
            await ConfigureBackendAsync(configuration.BackendBaseUrl);
        }
        catch
        {
            RuntimeChanged?.Invoke(null);
        }
    }
}
