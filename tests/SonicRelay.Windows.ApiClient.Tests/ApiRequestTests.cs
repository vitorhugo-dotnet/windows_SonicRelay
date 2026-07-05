using System.Net;
using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Devices;
using SonicRelay.Windows.ApiClient.Sessions;
using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.Tests;

public sealed class ApiRequestTests
{
    [Fact]
    public async Task LoginUsesIdentityRouteAndCamelCaseBody()
    {
        HttpRequestMessage? captured = null;
        string? body = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            captured = request;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"tokenType":"Bearer","accessToken":"access","expiresIn":900,"refreshToken":"refresh"}""");
        });
        var store = new MemoryTokenStore();

        var tokens = await new AuthApiClient(TestClient.Create(handler), store)
            .LoginAsync(new LoginRequest("user@example.com", "secret"));

        Assert.Equal(HttpMethod.Post, captured!.Method);
        Assert.Equal("/auth/login?useCookies=false", captured.RequestUri!.PathAndQuery);
        Assert.Equal("""{"email":"user@example.com","password":"secret"}""", body);
        Assert.Equal("access", tokens.AccessToken);
        Assert.Equal(tokens, store.Tokens);
    }

    [Fact]
    public async Task CurrentUserUsesBearerToken()
    {
        HttpRequestMessage? captured = null;
        var handler = new FakeHttpMessageHandler((request, _) =>
        {
            captured = request;
            return Task.FromResult(FakeHttpMessageHandler.Json(HttpStatusCode.OK,
                """{"id":"00000000-0000-0000-0000-000000000001","email":"u@example.com","displayName":"User","emailConfirmed":true,"createdAt":"2026-01-01T00:00:00Z","lastLoginAt":null}"""));
        });
        var store = new MemoryTokenStore(new TokenSet("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(5)));

        await new AuthApiClient(TestClient.Create(handler), store).GetCurrentUserAsync();

        Assert.Equal("/auth/me", captured!.RequestUri!.AbsolutePath);
        Assert.Equal("Bearer", captured.Headers.Authorization!.Scheme);
        Assert.Equal("access", captured.Headers.Authorization.Parameter);
    }

    [Fact]
    public async Task RegisterDeviceUsesPublisherShape()
    {
        string? body = null;
        string? path = null;
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            path = request.RequestUri!.AbsolutePath;
            body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return FakeHttpMessageHandler.Json(HttpStatusCode.Created,
                """{"id":"00000000-0000-0000-0000-000000000002","name":"Desktop","type":"windows_publisher","platform":"windows","publicKey":null,"trusted":false,"revoked":false,"lastSeenAt":null,"createdAt":"2026-01-01T00:00:00Z"}""");
        });
        var client = new DeviceApiClient(TestClient.Create(handler), ValidStore());

        await client.RegisterWindowsPublisherAsync(new RegisterDeviceRequest("Desktop", null));

        Assert.Equal("/api/devices/", path);
        Assert.Equal("""{"name":"Desktop","type":"windows_publisher","platform":"windows","publicKey":null}""", body);
    }

    [Fact]
    public async Task SessionOperationsUseDocumentedRoutesAndBodies()
    {
        var requests = new List<(HttpMethod Method, string Path, string? Body)>();
        var sessionId = Guid.Parse("00000000-0000-0000-0000-000000000003");
        var deviceId = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var response = """{"id":"00000000-0000-0000-0000-000000000003","ownerUserId":"00000000-0000-0000-0000-000000000001","sourceDeviceId":"00000000-0000-0000-0000-000000000002","status":"waiting","maxViewers":3,"codeExpiresAt":"2026-01-01T00:10:00Z","startedAt":null,"endedAt":null,"createdAt":"2026-01-01T00:00:00Z","code":"ABC123"}""";
        var handler = new FakeHttpMessageHandler(async (request, cancellationToken) =>
        {
            requests.Add((request.Method, request.RequestUri!.AbsolutePath,
                request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken)));
            return request.RequestUri!.AbsolutePath.EndsWith("/active", StringComparison.Ordinal)
                ? FakeHttpMessageHandler.Json(HttpStatusCode.OK, "[]")
                : FakeHttpMessageHandler.Json(HttpStatusCode.OK, response);
        });
        var client = new SessionApiClient(TestClient.Create(handler), ValidStore());

        await client.CreateSessionAsync(new CreateSessionRequest(deviceId, 3));
        await client.GetActiveSessionsAsync();
        await client.EndSessionAsync(sessionId);

        Assert.Equal((HttpMethod.Post, "/api/sessions/", """{"sourceDeviceId":"00000000-0000-0000-0000-000000000002","maxViewers":3}"""), requests[0]);
        Assert.Equal((HttpMethod.Get, "/api/sessions/active", null), requests[1]);
        Assert.Equal((HttpMethod.Post, $"/api/sessions/{sessionId}/end", null), requests[2]);
    }

    private static MemoryTokenStore ValidStore() =>
        new(new TokenSet("access", "refresh", DateTimeOffset.UtcNow.AddMinutes(5)));
}
