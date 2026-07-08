using SonicRelay.Windows.Core.Audio;

namespace SonicRelay.Windows.Core.Tests;

public sealed class AudioQualityStoreTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"sonic-audio-{Guid.NewGuid():N}.json");

    [Fact]
    public void Defaults_to_high_when_no_file_exists()
    {
        var store = new AudioQualityStore(path);

        Assert.Equal(AudioQualityProfile.High, store.CurrentProfile);
    }

    [Fact]
    public async Task Round_trips_a_custom_profile()
    {
        var custom = AudioQualityProfile.Custom(channels: 1, bitrateKbps: 48, frameDurationMs: 40);
        var store = new AudioQualityStore(path);

        await store.SetProfileAsync(custom);

        Assert.Equal(custom, store.CurrentProfile);
        var reloaded = new AudioQualityStore(path);
        Assert.Equal(custom, reloaded.CurrentProfile);
    }

    [Fact]
    public async Task Persists_a_preset_selection()
    {
        var store = new AudioQualityStore(path);

        await store.SetProfileAsync(AudioQualityProfile.Voice);

        Assert.Equal(AudioQualityProfile.Voice, new AudioQualityStore(path).CurrentProfile);
    }

    [Fact]
    public async Task Corrupt_file_falls_back_to_default()
    {
        await File.WriteAllTextAsync(path, "{ this is not valid json ]");

        var store = new AudioQualityStore(path);

        Assert.Equal(AudioQualityProfile.Default, store.CurrentProfile);
    }

    [Fact]
    public async Task Rejects_an_invalid_profile()
    {
        var store = new AudioQualityStore(path);
        var invalid = AudioQualityProfile.High with { OpusBitrateKbps = 5000 };

        await Assert.ThrowsAsync<ArgumentException>(() => store.SetProfileAsync(invalid));
    }

    public void Dispose()
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
