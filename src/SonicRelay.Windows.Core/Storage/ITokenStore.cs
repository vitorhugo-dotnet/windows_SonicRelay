namespace SonicRelay.Windows.Core.Storage;

public interface ITokenStore
{
    Task<TokenStorageResult> SaveAsync(TokenSet tokens, CancellationToken cancellationToken = default);
    Task<TokenStorageResult> LoadAsync(CancellationToken cancellationToken = default);
    Task<TokenStorageResult> DeleteAsync(CancellationToken cancellationToken = default);
}

