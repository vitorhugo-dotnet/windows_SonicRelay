using SonicRelay.Windows.Core;

namespace SonicRelay.Windows.Core.Tests;

public sealed class ProjectAssemblyTests
{
    [Fact]
    public void CoreMarkerBelongsToCoreAssembly()
    {
        Assert.Equal("SonicRelay.Windows.Core", typeof(ProjectAssembly).Assembly.GetName().Name);
    }
}
