using System.Net;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Errors;

namespace SonicRelay.Windows.ApiClient.Tests;

public sealed class ApiErrorTests
{
    public static TheoryData<HttpStatusCode, ApiErrorKind> StatusCases => new()
    {
        { HttpStatusCode.Unauthorized, ApiErrorKind.Unauthorized },
        { HttpStatusCode.Forbidden, ApiErrorKind.Forbidden },
        { HttpStatusCode.BadRequest, ApiErrorKind.Validation },
        { HttpStatusCode.UnprocessableEntity, ApiErrorKind.Validation },
        { HttpStatusCode.Conflict, ApiErrorKind.Conflict },
        { HttpStatusCode.ServiceUnavailable, ApiErrorKind.BackendUnavailable },
        { HttpStatusCode.NotFound, ApiErrorKind.Unknown }
    };

    [Theory]
    [MemberData(nameof(StatusCases))]
    public async Task MapsHttpStatus(HttpStatusCode status, ApiErrorKind expected)
    {
        var handler = new FakeHttpMessageHandler((_, _) => Task.FromResult(
            FakeHttpMessageHandler.Json(status, """{"error":"safe message"}""")));

        var error = await Assert.ThrowsAsync<ApiClientException>(() =>
            new DeviceApiClient(TestClient.Create(handler), new MemoryTokenStore()).GetDevicesAsync());

        Assert.Equal(expected, error.Kind);
        Assert.DoesNotContain("token", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MapsConnectionFailureToNetworkUnavailable()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new HttpRequestException("offline"));

        var error = await Assert.ThrowsAsync<ApiClientException>(() =>
            new DeviceApiClient(TestClient.Create(handler), new MemoryTokenStore()).GetDevicesAsync());

        Assert.Equal(ApiErrorKind.NetworkUnavailable, error.Kind);
    }

    [Fact]
    public async Task MapsTimeoutToBackendUnavailable()
    {
        var handler = new FakeHttpMessageHandler((_, _) => throw new TaskCanceledException("timeout"));

        var error = await Assert.ThrowsAsync<ApiClientException>(() =>
            new DeviceApiClient(TestClient.Create(handler), new MemoryTokenStore()).GetDevicesAsync());

        Assert.Equal(ApiErrorKind.BackendUnavailable, error.Kind);
    }
}
