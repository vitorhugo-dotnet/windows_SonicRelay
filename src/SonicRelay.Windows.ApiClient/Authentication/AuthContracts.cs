using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient.Authentication;

public interface IAuthApiClient
{
    Task<TokenSet> LoginAsync(LoginRequest request, CancellationToken cancellationToken = default);
    Task<TokenSet> RefreshAsync(string refreshToken, CancellationToken cancellationToken = default);
    Task<CurrentUserResponse> GetCurrentUserAsync(CancellationToken cancellationToken = default);
}

public sealed record LoginRequest(string Email, string Password);

public sealed record CurrentUserResponse(
    Guid Id,
    string? Email,
    string? DisplayName,
    bool EmailConfirmed,
    DateTimeOffset CreatedAt,
    DateTimeOffset? LastLoginAt);

internal sealed record RefreshTokenRequest(string RefreshToken);

internal sealed record IdentityTokenResponse(
    string TokenType,
    string AccessToken,
    int ExpiresIn,
    string RefreshToken)
{
    public TokenSet ToTokenSet() =>
        new(AccessToken, RefreshToken, DateTimeOffset.UtcNow.AddSeconds(ExpiresIn));
}
