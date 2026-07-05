namespace SonicRelay.Windows.Core.Storage;

public enum TokenStorageStatus
{
    Success,
    SecureStorageUnavailable,
    Failed
}

public sealed record TokenStorageResult(TokenStorageStatus Status, TokenSet? Tokens = null, string? Message = null)
{
    public bool Succeeded => Status == TokenStorageStatus.Success;

    public static TokenStorageResult Success(TokenSet? tokens = null) => new(TokenStorageStatus.Success, tokens);
    public static TokenStorageResult SecureStorageUnavailable(string message) => new(TokenStorageStatus.SecureStorageUnavailable, null, message);
    public static TokenStorageResult Failed(string message) => new(TokenStorageStatus.Failed, null, message);
}

