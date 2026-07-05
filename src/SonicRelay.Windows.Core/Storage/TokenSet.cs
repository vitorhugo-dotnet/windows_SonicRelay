namespace SonicRelay.Windows.Core.Storage;

public sealed record TokenSet(string AccessToken, string RefreshToken, DateTimeOffset ExpiresAtUtc);

