using System.Text;
using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.Core.Tests;

public sealed class UserScopedTokenStoreTests : IDisposable
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), $"SonicRelay-token-{Guid.NewGuid():N}");

    [Fact]
    public async Task SaveLoadAndDeleteUseProtectedFile()
    {
        var store = new UserScopedTokenStore(_directory, new PrefixProtector());
        var tokens = new TokenSet("access-secret", "refresh-secret", new DateTimeOffset(2030, 1, 1, 0, 0, 0, TimeSpan.Zero));

        Assert.True((await store.SaveAsync(tokens)).Succeeded);
        var bytes = await File.ReadAllBytesAsync(Path.Combine(_directory, "tokens.dat"));
        Assert.StartsWith("protected:", Encoding.UTF8.GetString(bytes));
        Assert.DoesNotContain("access-secret", Encoding.UTF8.GetString(bytes));
        Assert.Equal(tokens, (await store.LoadAsync()).Tokens);
        Assert.True((await store.DeleteAsync()).Succeeded);
        Assert.False(File.Exists(Path.Combine(_directory, "tokens.dat")));
    }

    [Fact]
    public async Task SecureStorageFailureDoesNotWritePlaintextOrLeakTokens()
    {
        var store = new UserScopedTokenStore(_directory, new FailingProtector());
        var result = await store.SaveAsync(new TokenSet("access-secret", "refresh-secret", DateTimeOffset.UtcNow));

        Assert.Equal(TokenStorageStatus.SecureStorageUnavailable, result.Status);
        Assert.DoesNotContain("access-secret", result.Message ?? string.Empty);
        Assert.DoesNotContain("refresh-secret", result.Message ?? string.Empty);
        Assert.False(File.Exists(Path.Combine(_directory, "tokens.dat")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, true);
    }

    private sealed class PrefixProtector : ITokenProtector
    {
        public byte[] Protect(byte[] plaintext) => Encoding.UTF8.GetBytes("protected:" + Convert.ToBase64String(plaintext));
        public byte[] Unprotect(byte[] ciphertext) => Convert.FromBase64String(Encoding.UTF8.GetString(ciphertext)[10..]);
    }

    private sealed class FailingProtector : ITokenProtector
    {
        public byte[] Protect(byte[] plaintext) => throw new SecureStorageUnavailableException("DPAPI unavailable");
        public byte[] Unprotect(byte[] ciphertext) => throw new SecureStorageUnavailableException("DPAPI unavailable");
    }
}
