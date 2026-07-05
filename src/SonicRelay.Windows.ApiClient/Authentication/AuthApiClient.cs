using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.Authentication;

public sealed class AuthApiClient(HttpClient httpClient, ITokenStore tokenStore) : IAuthApiClient
{
    private readonly ApiHttpClient _api = new(httpClient, tokenStore);

    public async Task<TokenSet> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default)
    {
        var response = await _api.SendAsync<IdentityTokenResponse>(
            HttpMethod.Post,
            "/auth/login?useCookies=false",
            request,
            authenticated: false,
            cancellationToken,
            allowRefresh: false);
        var tokens = response.ToTokenSet();
        await _api.SaveTokensAsync(tokens, cancellationToken);
        return tokens;
    }

    public Task<TokenSet> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default) =>
        _api.RefreshTokensAsync(refreshToken, cancellationToken);

    public Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default) =>
        _api.SendAsync<CurrentUserResponse>(HttpMethod.Get, "/auth/me", null, true, cancellationToken);
}
