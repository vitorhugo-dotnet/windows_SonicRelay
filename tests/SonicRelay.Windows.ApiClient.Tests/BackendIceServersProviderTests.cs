using SonicRelay.Windows.ApiClient.Errors;
using SonicRelay.Windows.ApiClient.WebRtc;
using SonicRelay.Windows.WebRtc;

namespace SonicRelay.Windows.ApiClient.Tests;

public sealed class BackendIceServersProviderTests
{
    [Fact]
    public async Task Maps_backend_response_to_ice_servers()
    {
        var api = new StubWebRtcApiClient(new IceServersResponse(
            [
                new IceServerResponse(["stun:stun.example.com:3478"]),
                new IceServerResponse(["turn:relay.example.com:3478"], "1751900000:user", "secret==")
            ], 3600));
        var provider = new BackendIceServersProvider(api);

        var servers = await provider.GetIceServersAsync();

        Assert.Equal(2, servers.Count);
        Assert.Equal("turn:relay.example.com:3478", servers[1].Urls[0]);
        Assert.Equal("1751900000:user", servers[1].Username);
        Assert.Equal("secret==", servers[1].Credential);
    }

    [Fact]
    public async Task Caches_until_ttl_minus_safety_margin()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var api = new StubWebRtcApiClient(new IceServersResponse([new IceServerResponse(["turn:relay:3478"], "u", "c")], 3600));
        var provider = new BackendIceServersProvider(api, time);

        await provider.GetIceServersAsync();
        time.Advance(TimeSpan.FromSeconds(3600 - 60 - 1));
        await provider.GetIceServersAsync();
        Assert.Equal(1, api.CallCount);

        time.Advance(TimeSpan.FromSeconds(2));
        await provider.GetIceServersAsync();
        Assert.Equal(2, api.CallCount);
    }

    [Fact]
    public async Task Falls_back_to_stun_when_backend_fails_with_no_cache()
    {
        var api = new StubWebRtcApiClient(new ApiClientException(ApiErrorKind.BackendUnavailable, "down"));
        var provider = new BackendIceServersProvider(api);

        var servers = await provider.GetIceServersAsync();

        var only = Assert.Single(servers);
        Assert.StartsWith("stun:", only.Urls[0]);
    }

    [Fact]
    public async Task Returns_last_good_cache_when_a_later_refresh_fails()
    {
        var time = new FakeTimeProvider(DateTimeOffset.UnixEpoch);
        var api = new StubWebRtcApiClient(new IceServersResponse([new IceServerResponse(["turn:relay:3478"], "u", "c")], 3600));
        var provider = new BackendIceServersProvider(api, time);
        await provider.GetIceServersAsync();

        api.Fail(new ApiClientException(ApiErrorKind.NetworkUnavailable, "offline"));
        time.Advance(TimeSpan.FromHours(2)); // force a refresh attempt

        var servers = await provider.GetIceServersAsync();
        Assert.Equal("turn:relay:3478", servers[0].Urls[0]);
    }

    private sealed class StubWebRtcApiClient : IWebRtcApiClient
    {
        private IceServersResponse? response;
        private Exception? failure;

        public StubWebRtcApiClient(IceServersResponse response) => this.response = response;
        public StubWebRtcApiClient(Exception failure) => this.failure = failure;

        public int CallCount { get; private set; }

        public void Fail(Exception exception) { failure = exception; response = null; }

        public Task<IceServersResponse> GetIceServersAsync(CancellationToken cancellationToken = default)
        {
            CallCount++;
            if (failure is not null) return Task.FromException<IceServersResponse>(failure);
            return Task.FromResult(response!);
        }
    }

    private sealed class FakeTimeProvider(DateTimeOffset start) : TimeProvider
    {
        private DateTimeOffset now = start;
        public override DateTimeOffset GetUtcNow() => now;
        public void Advance(TimeSpan by) => now = now.Add(by);
    }
}
