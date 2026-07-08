using SonicRelay.Windows.Core.Configuration;

namespace SonicRelay.Windows.Core.Tests;

public sealed class RelayPreferenceStoreTests : IDisposable
{
    private readonly string path = Path.Combine(Path.GetTempPath(), $"sonicrelay-prefs-{Guid.NewGuid():N}.json");

    [Fact]
    public void DefaultsToDirectWhenNoFileExists()
    {
        Assert.False(new RelayPreferenceStore(path).ForceRelay);
    }

    [Fact]
    public async Task PersistsForceRelayAcrossInstances()
    {
        await new RelayPreferenceStore(path).SetForceRelayAsync(true);

        // A fresh instance reads the persisted preference back.
        Assert.True(new RelayPreferenceStore(path).ForceRelay);

        await new RelayPreferenceStore(path).SetForceRelayAsync(false);
        Assert.False(new RelayPreferenceStore(path).ForceRelay);
    }

    public void Dispose()
    {
        if (File.Exists(path)) File.Delete(path);
    }
}
