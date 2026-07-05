using System.Net;
using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.Tests;

internal sealed class FakeHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> send)
    : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken) =>
        send(request, cancellationToken);

    public static HttpResponseMessage Json(HttpStatusCode status, string json) => new(status)
    {
        Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
    };
}

internal sealed class MemoryTokenStore(TokenSet? initial = null) : ITokenStore
{
    public TokenSet? Tokens { get; private set; } = initial;

    public Task<TokenStorageResult> SaveAsync(TokenSet tokens, CancellationToken cancellationToken = default)
    {
        Tokens = tokens;
        return Task.FromResult(TokenStorageResult.Success(tokens));
    }

    public Task<TokenStorageResult> LoadAsync(CancellationToken cancellationToken = default) =>
        Task.FromResult(TokenStorageResult.Success(Tokens));

    public Task<TokenStorageResult> DeleteAsync(CancellationToken cancellationToken = default)
    {
        Tokens = null;
        return Task.FromResult(TokenStorageResult.Success());
    }
}

internal static class TestClient
{
    public static HttpClient Create(HttpMessageHandler handler) => new(handler)
    {
        BaseAddress = new Uri("https://backend.example/")
    };
}
