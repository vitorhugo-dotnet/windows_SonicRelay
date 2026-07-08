using System;
using System.Globalization;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using SonicRelay.Windows.Audio;
using SonicRelay.Windows.Core.Audio;
using SonicRelay.Windows.Presentation;

namespace SonicRelay.Windows.App.Pages;

public sealed partial class AudioPage : Page
{
    private const string CustomTag = "custom";
    private PublisherWorkflow? workflow;
    private AudioQualityStore? audioQuality;
    private bool suppressQualitySelection;

    public AudioPage() { InitializeComponent(); Loaded += OnLoaded; Unloaded += OnUnloaded; }
    private void OnLoaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged += RuntimeChanged; Attach(App.CurrentApp.Runtime); }
    private void OnUnloaded(object sender, RoutedEventArgs e) { App.CurrentApp.RuntimeChanged -= RuntimeChanged; Attach(null); }
    private void RuntimeChanged(PublisherRuntime? runtime) => DispatcherQueue.TryEnqueue(() => Attach(runtime));

    private void Attach(PublisherRuntime? runtime)
    {
        if (workflow is not null) workflow.StateChanged -= StateChanged;
        workflow = runtime?.Workflow;
        audioQuality = runtime?.AudioQuality;
        if (workflow is not null) workflow.StateChanged += StateChanged;
        PopulateQualityCombo();
        Render(workflow?.State);
    }

    private async void Start_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.StartAudioAsync(); }
    private void Pause_Click(object sender, RoutedEventArgs e) { }
    private async void Stop_Click(object sender, RoutedEventArgs e) { if (workflow is not null) await workflow.StopAudioAsync(); }
    private void StateChanged(PublisherSnapshot state) => DispatcherQueue.TryEnqueue(() => Render(state));

    private void Render(PublisherSnapshot? state)
    {
        var diagnostics = state?.AudioDiagnostics;
        var audioState = state?.AudioState ?? AudioCaptureState.Stopped;
        StateText.Text = audioState == AudioCaptureState.Recovering
            ? "Reconnecting audio…"
            : audioState.ToString();
        DeviceText.Text = diagnostics?.Device?.Name ?? "—";
        FormatText.Text = diagnostics?.Device is { } device ? $"{device.SampleRate:N0} Hz / {device.ChannelCount} channels / {device.Format}" : "—";
        LevelMeter.Value = (diagnostics?.Level.Peak ?? 0) * 100;
        CapturedText.Text = $"{diagnostics?.FramesCaptured ?? 0:N0} frames / {diagnostics?.BytesCaptured ?? 0:N0} bytes";
        ErrorText.Text = state?.ErrorMessage ?? diagnostics?.LastError?.Message ?? "—";
        StartButton.IsEnabled = state?.CanStartAudio == true;
        PauseButton.Visibility = Visibility.Collapsed;
        StopButton.IsEnabled = state?.CanStopAudio == true;

        // Block quality changes while capture is running; a restart applies them.
        var capturing = state?.CanStopAudio == true;
        var enabled = audioQuality is not null && !capturing;
        QualityCombo.IsEnabled = enabled;
        CustomChannels.IsEnabled = enabled;
        CustomBitrate.IsEnabled = enabled;
        CustomFrame.IsEnabled = enabled;
        CustomApplyButton.IsEnabled = enabled;
        QualityHintText.Text = capturing
            ? "Stop and restart capture to apply a different profile."
            : "The selected profile applies when capture starts.";
    }

    private void PopulateQualityCombo()
    {
        if (audioQuality is null) { QualityCombo.Items.Clear(); RenderEffective(null); return; }

        suppressQualitySelection = true;
        QualityCombo.Items.Clear();
        foreach (var preset in AudioQualityProfile.Presets)
        {
            QualityCombo.Items.Add(new ComboBoxItem { Content = preset.DisplayName, Tag = preset.Id });
        }
        QualityCombo.Items.Add(new ComboBoxItem { Content = "Custom", Tag = CustomTag });

        var current = audioQuality.CurrentProfile;
        var selectedTag = current.IsPreset ? current.Id : CustomTag;
        SelectComboByTag(QualityCombo, selectedTag);
        CustomPanel.Visibility = current.IsPreset ? Visibility.Collapsed : Visibility.Visible;
        if (!current.IsPreset) LoadCustomFields(current);
        suppressQualitySelection = false;

        RenderEffective(current);
    }

    private async void Quality_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (suppressQualitySelection || audioQuality is null) return;
        var tag = (QualityCombo.SelectedItem as ComboBoxItem)?.Tag as string;
        if (tag is null) return;

        if (string.Equals(tag, CustomTag, StringComparison.Ordinal))
        {
            CustomPanel.Visibility = Visibility.Visible;
            LoadCustomFields(audioQuality.CurrentProfile);
            return; // Custom is applied explicitly via the Apply button.
        }

        CustomPanel.Visibility = Visibility.Collapsed;
        CustomErrorText.Visibility = Visibility.Collapsed;
        var preset = AudioQualityProfile.FromId(tag);
        if (preset is null) return;
        await audioQuality.SetProfileAsync(preset);
        RenderEffective(preset);
    }

    private async void CustomApply_Click(object sender, RoutedEventArgs e)
    {
        if (audioQuality is null) return;
        try
        {
            var channels = ReadTagInt(CustomChannels, 2);
            var frame = ReadTagInt(CustomFrame, 20);
            var bitrate = (int)Math.Round(double.IsNaN(CustomBitrate.Value) ? 96 : CustomBitrate.Value);
            var profile = AudioQualityProfile.Custom(channels, bitrate, frame);
            await audioQuality.SetProfileAsync(profile);
            CustomErrorText.Visibility = Visibility.Collapsed;
            RenderEffective(profile);
        }
        catch (ArgumentException exception)
        {
            CustomErrorText.Text = exception.Message;
            CustomErrorText.Visibility = Visibility.Visible;
        }
    }

    private void LoadCustomFields(AudioQualityProfile profile)
    {
        SelectComboByTag(CustomChannels, profile.Channels.ToString(CultureInfo.InvariantCulture));
        SelectComboByTag(CustomFrame, profile.FrameDurationMs.ToString(CultureInfo.InvariantCulture));
        CustomBitrate.Value = profile.OpusBitrateKbps;
    }

    private void RenderEffective(AudioQualityProfile? profile)
    {
        if (profile is null)
        {
            BitrateText.Text = ChannelsText.Text = FrameText.Text = SampleRateText.Text = EstimateText.Text = "—";
            return;
        }
        BitrateText.Text = $"{profile.OpusBitrateKbps} kbps";
        ChannelsText.Text = profile.Channels == 1 ? "Mono (1)" : "Stereo (2)";
        FrameText.Text = $"{profile.FrameDurationMs} ms";
        SampleRateText.Text = $"{profile.SampleRateHz:N0} Hz";
        var estimate = profile.EstimateTraffic();
        EstimateText.Text = string.Format(
            CultureInfo.InvariantCulture,
            "{0} kbps · {1:0.0} MB/min · {2:0.0} MB/h",
            estimate.Kbps, estimate.MegabytesPerMinute, estimate.MegabytesPerHour);
    }

    private static void SelectComboByTag(ComboBox combo, string tag)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is ComboBoxItem item && item.Tag as string == tag)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private static int ReadTagInt(ComboBox combo, int fallback)
    {
        if (combo.SelectedItem is ComboBoxItem item && item.Tag is string tag
            && int.TryParse(tag, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
        {
            return value;
        }
        return fallback;
    }
}
