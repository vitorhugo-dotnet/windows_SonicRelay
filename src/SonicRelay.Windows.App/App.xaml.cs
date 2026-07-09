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

    /// <summary>
    /// App-global background/tray preferences (issue #26). These are independent of
    /// the configured backend, so they live on the app rather than the runtime.
    /// </summary>
    public TrayBackgroundPreferenceStore TrayPreferences { get; } = new();

    public App()
    {
        InitializeComponent();
    }

    protected override void OnLaunched(LaunchActivatedEventArgs args)
    {
        _window = new MainWindow();
        _window.Activate();
        // "Start minimized to tray": keep the process running but never surface the
        // window on launch. Only honoured when the app is allowed to live in the tray.
        if (TrayPreferences.StartMinimized && TrayPreferences.KeepRunningInTray && _window is MainWindow main)
        {
            main.HideToTrayOnStartup();
        }

        _ = LoadConfiguredRuntimeAsync();
    }

    /// <summary>
    /// Swaps the active runtime for one pointed at <paramref name="backendBaseUrl"/>
    /// and persists that URL so the next launch restores the session against the
    /// same backend (instead of the localhost template written on first run).
    /// Set <paramref name="restoreSession"/> to false when the caller signs in
    /// immediately afterwards — running the startup restore concurrently with an
    /// explicit login is redundant and the two used to race for the workflow lock.
    /// </summary>
    public async Task ConfigureBackendAsync(Uri backendBaseUrl, bool restoreSession = true)
    {
        var replacement = PublisherRuntime.Create(backendBaseUrl);
        var previous = runtime;
        runtime = replacement;
        RuntimeChanged?.Invoke(runtime);
        if (previous is not null) await previous.DisposeAsync();
        try
        {
            await new UserConfigurationLoader().SaveBackendAsync(backendBaseUrl);
        }
        catch
        {
            // Persisting the backend is best-effort; the session still works.
        }
        // Restore a persisted session (refresh + /auth/me) so the user stays signed
        // in across restarts. Non-blocking; the UI reacts to workflow StateChanged.
        if (restoreSession) _ = replacement.Workflow.RestoreSessionAsync();
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
