using SonicRelay.Windows.ApiClient;

namespace SonicRelay.Windows.ApiClient.Tests;

public sealed class ProjectAssemblyTests
{
    [Fact]
    public void ApiClientMarkerBelongsToApiClientAssembly()
    {
        Assert.Equal("SonicRelay.Windows.ApiClient", typeof(ProjectAssembly).Assembly.GetName().Name);
    }
}
