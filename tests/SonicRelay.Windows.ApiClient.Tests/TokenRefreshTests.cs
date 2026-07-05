using System.Net;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.Tests;

public sealed class TokenRefreshTests
{
    [Fact]
    public async Task UnauthorizedRequestRefreshesPersistsAndRetriesOnce()
    {
        var calls = 0;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            calls++;
            if (calls == 1)
            {
                Assert.Equal("old-access", request.Headers.Authorization!.Parameter);
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            if (calls == 2)
            {
                Assert.Equal("/auth/refresh", request.RequestUri!.AbsolutePath);
                Assert.Equal("""{"refreshToken":"old-refresh"}""", await request.Content!.ReadAsStringAsync(cancellationToken));
                return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                    """{"tokenType":"Bearer","accessToken":"new-access","expiresIn":900,"refreshToken":"new-refresh"}""");
            }

            Assert.Equal("new-access", request.Headers.Authorization!.Parameter);
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]");
        });
        var store = new MemoryTokenStore(new TokenSet("old-access", "old-refresh", DateTimeOffset.UtcNow.AddMinutes(-1)));

        await new DeviceApiClient(TestClient.Create(handler), store).GetDevicesAsync();

        Assert.Equal(3, calls);
        Assert.Equal("new-access", store.Tokens!.AccessToken);
        Assert.Equal("new-refresh", store.Tokens.RefreshToken);
    }
}
