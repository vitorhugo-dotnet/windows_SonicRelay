using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.Core.Tests;

public sealed class TokenStoreContractTests
{
    [Fact]
    public async Task TestStoreSavesLoadsAndDeletesTokens()
    {
        ITokenStore store = new TestTokenStore();
        var tokens = new TokenSet("access-secret", "refresh-secret", DateTimeOffset.UtcNow.AddHours(1));

        Assert.True((await store.SaveAsync(tokens)).Succeeded);
        Assert.Equal(tokens, (await store.LoadAsync()).Tokens);
        Assert.True((await store.DeleteAsync()).Succeeded);
        Assert.Null((await store.LoadAsync()).Tokens);
    }

    private sealed class TestTokenStore : ITokenStore
    {
        private TokenSet? _tokens;

        public Task<TokenStorageResult> SaveAsync(TokenSet tokens, CancellationToken cancellationToken = default)
        {
            _tokens = tokens;
            return Task.FromResult(TokenStorageResult.Success());
        }

        public Task<TokenStorageResult> LoadAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(TokenStorageResult.Success(_tokens));

        public Task<TokenStorageResult> DeleteAsync(CancellationToken cancellationToken = default)
        {
            _tokens = null;
            return Task.FromResult(TokenStorageResult.Success());
        }
    }
}

