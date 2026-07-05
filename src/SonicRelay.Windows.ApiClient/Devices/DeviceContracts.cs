namespace SonicRelay.Windows.ApiClient.Devices;

public interface IDeviceApiClient
{
    Task<DeviceResponse> RegisterWindowsPublisherAsync(
        RegisterDeviceRequest request,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DeviceResponse>> GetDevicesAsync(CancellationToken cancellationToken = default);
}

public sealed record RegisterDeviceRequest(string Name, string? PublicKey);

internal sealed record CreateDeviceRequest(string Name, string Type, string Platform, string? PublicKey);

public sealed record DeviceResponse(
    Guid Id,
    string Name,
    string Type,
    string Platform,
    string? PublicKey,
    bool Trusted,
    bool Revoked,
    DateTimeOffset? LastSeenAt,
    DateTimeOffset CreatedAt);
