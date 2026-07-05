using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class AudioPage : Page
{
    private PublisherWorkflow? workflow;
    public AudioPage() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged += RuntimeChanged; Attach(App.CurrentApp.Runtime); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged -= RuntimeChanged; Attach(null); }
    private void RuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));
    private void Attach(PublisherRuntime? runtime) { if (workflow is not null) workflow.StateChanged -= StateChanged; workflow = runtime?.Workflow; if (workflow is not null) workflow.StateChanged += StateChanged; Render(workflow?.State); }
    private async void Start_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.StartAudioAsync(); }
    private void Pause_Click(object sender, RoutedEventArgs e) { }
    private async void Stop_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.StopAudioAsync(); }
    private void StateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));
    private void Render(PublisherSnapshot? state)
    {
        var diagnostics = state?.AudioDiagnostics;
        StateText.Text = (state?.AudioState ?? AudioCaptureState.Stopped).ToString();
        DeviceText.Text = diagnostics?.Device?.Name ?? "—";
        FormatText.Text = diagnostics?.Device is { } device ? $"{device.SampleRate:N0} Hz / {device.ChannelCount} channels / {device.Format}" : "—";
        LevelMeter.Value = (diagnostics?.Level.Peak ?? 0) * 100;
        CapturedText.Text = $"{diagnostics?.FramesCaptured ?? 0:N0} frames / {diagnostics?.BytesCaptured ?? 0:N0} bytes";
        ErrorText.Text = state?.ErrorMessage ?? diagnostics?.LastError?.Message ?? "—";
        StartButton.IsEnabled = state?.CanStartAudio == true;
        PauseButton.Visibility = Visibility.Collapsed;
        StopButton.IsEnabled = state?.CanStopAudio == true;
    }
}
