using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.Core.Tests;

public sealed class AudioOutputPreferenceStoreTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"sonic-output-{Guid.NewGuid():N}.json");

    [Fact]
    public void Defaults_to_system_default_when_no_file_exists()
    {
        var store = new AudioOutputPreferenceStore(path);

        Assert.Null(store.SelectedDeviceId);
        Assert.Null(store.SelectedDeviceName);
    }

    [Fact]
    public async Task Round_trips_a_selected_device()
    {
        var store = new AudioOutputPreferenceStore(path);

        await store.SetSelectedDeviceAsync("{0.0.0.00000000}.{guid}", "Speakers (Realtek)");

        Assert.Equal("{0.0.0.00000000}.{guid}", store.SelectedDeviceId);
        var reloaded = new AudioOutputPreferenceStore(path);
        Assert.Equal("{0.0.0.00000000}.{guid}", reloaded.SelectedDeviceId);
        Assert.Equal("Speakers (Realtek)", reloaded.SelectedDeviceName);
    }

    [Fact]
    public async Task Clearing_the_selection_returns_to_default()
    {
        var store = new AudioOutputPreferenceStore(path);
        await store.SetSelectedDeviceAsync("id", "name");

        await store.SetSelectedDeviceAsync(null, null);

        Assert.Null(store.SelectedDeviceId);
        Assert.Null(new AudioOutputPreferenceStore(path).SelectedDeviceId);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_default()
    {
        await File.WriteAllTextAsync(path, "not json at all }");

        var store = new AudioOutputPreferenceStore(path);

        Assert.Null(store.SelectedDeviceId);
    }

    public void Dispose()
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
