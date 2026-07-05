using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Audio;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class AudioPage : Page, IAsyncDisposable
{
    private readonly AudioCaptureService _capture = new();

    public AudioPage()
    {
        InitializeComponent();
        _capture.StateChanged += CaptureChanged;
        _capture.LevelChanged += LevelChanged;
        Unloaded += Page_Unloaded;
    }

    private async void Start_Click(object sender, RoutedEventArgs e) => await _capture.StartAsync();

    private async void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (_capture.State == AudioCaptureState.Paused) await _capture.ResumeAsync();
        else await _capture.PauseAsync();
    }

    private async void Stop_Click(object sender, RoutedEventArgs e) => await _capture.StopAsync();

    private void CaptureChanged(AudioCaptureState state) => DispatcherQueue.TryEnqueue(RefreshDiagnostics);

    private void LevelChanged(AudioLevelSnapshot level) => DispatcherQueue.TryEnqueue(RefreshDiagnostics);

    private void RefreshDiagnostics()
    {
        var diagnostics = _capture.Diagnostics;
        StateText.Text = diagnostics.State.ToString();
        DeviceText.Text = diagnostics.Device?.Name ?? "—";
        FormatText.Text = diagnostics.Device is { } device
            ? $"{device.SampleRate:N0} Hz / {device.ChannelCount} channels / {device.Format}"
            : "—";
        LevelMeter.Value = diagnostics.Level.Peak * 100;
        CapturedText.Text = $"{diagnostics.FramesCaptured:N0} frames / {diagnostics.BytesCaptured:N0} bytes";
        ErrorText.Text = diagnostics.LastError?.Message ?? "—";
        StartButton.IsEnabled = diagnostics.State is AudioCaptureState.Stopped or AudioCaptureState.Faulted;
        PauseButton.IsEnabled = diagnostics.State is AudioCaptureState.Capturing or AudioCaptureState.Paused;
        PauseButton.Content = diagnostics.State == AudioCaptureState.Paused ? "Resume" : "Pause";
        StopButton.IsEnabled = diagnostics.State is AudioCaptureState.Capturing or AudioCaptureState.Paused or AudioCaptureState.Faulted;
    }

    private async void Page_Unloaded(object sender, RoutedEventArgs e) => await DisposeAsync();

    public ValueTask DisposeAsync() => _capture.DisposeAsync();
}
