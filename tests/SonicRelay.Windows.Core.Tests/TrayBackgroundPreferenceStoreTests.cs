using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.Core.Tests;

public sealed class TrayBackgroundPreferenceStoreTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"sonic-tray-{Guid.NewGuid():N}.json");

    [Fact]
    public void Defaults_keep_in_tray_on_and_others_off_or_on()
    {
        var store = new TrayBackgroundPreferenceStore(path);

        Assert.True(store.KeepRunningInTray);
        Assert.False(store.StartMinimized);
        Assert.True(store.ShowNotifications);
    }

    [Fact]
    public async Task Round_trips_all_flags()
    {
        var store = new TrayBackgroundPreferenceStore(path);

        await store.UpdateAsync(keepRunningInTray: false, startMinimized: true, showNotifications: false);

        var reloaded = new TrayBackgroundPreferenceStore(path);
        Assert.False(reloaded.KeepRunningInTray);
        Assert.True(reloaded.StartMinimized);
        Assert.False(reloaded.ShowNotifications);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_defaults()
    {
        await File.WriteAllTextAsync(path, "not json {{");

        var store = new TrayBackgroundPreferenceStore(path);

        Assert.True(store.KeepRunningInTray);
        Assert.True(store.ShowNotifications);
    }

    public void Dispose()
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
