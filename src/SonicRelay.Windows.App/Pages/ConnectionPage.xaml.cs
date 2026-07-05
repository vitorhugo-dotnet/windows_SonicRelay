using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class ConnectionPage : Page
{
    private PublisherWorkflow? workflow;

    public ConnectionPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        App.CurrentApp.RuntimeChanged += OnRuntimeChanged;
        Attach(App.CurrentApp.Runtime);
    }

    private void OnUnloaded(object sender, RoutedEventArgs e)
    {
        App.CurrentApp.RuntimeChanged -= OnRuntimeChanged;
        Attach(null);
    }

    private void OnRuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));

    private void Attach(PublisherRuntime? runtime)
    {
        if (workflow is not null) workflow.StateChanged -= OnStateChanged;
        workflow = runtime?.Workflow;
        if (workflow is not null) workflow.StateChanged += OnStateChanged;
        Render(workflow?.State);
    }

    private async void Login_Click(object sender, RoutedEventArgs e)
    {
        ErrorBar.IsOpen = false;
        if (!Uri.TryCreate(BackendUrlBox.Text?.Trim(), UriKind.Absolute, out var backend)
            || backend.Scheme is not ("http" or "https"))
        {
            ErrorBar.Message = "Backend URL is required and must use HTTP or HTTPS.";
            ErrorBar.IsOpen = true;
            return;
        }
        try
        {
            await App.CurrentApp.ConfigureBackendAsync(backend);
            await App.CurrentApp.Runtime!.Workflow.LoginAsync(EmailBox.Text, PasswordBox.Password);
            PasswordBox.Password = string.Empty;
        }
        catch (Exception exception)
        {
            ErrorBar.Message = exception.Message;
            ErrorBar.IsOpen = true;
        }
    }

    private void OnStateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));

    private void Render(PublisherSnapshot? state)
    {
        AuthStatusText.Text = state?.IsAuthenticated == true ? state.UserDisplayName ?? "Signed in" : "Not signed in";
        DeviceStatusText.Text = state?.DeviceName ?? "Not registered";
        BusyRing.IsActive = state?.IsBusy == true;
        LoginButton.IsEnabled = state?.IsBusy != true;
        ErrorBar.Message = state?.ErrorMessage ?? string.Empty;
        ErrorBar.IsOpen = !string.IsNullOrWhiteSpace(state?.ErrorMessage);
    }
}
