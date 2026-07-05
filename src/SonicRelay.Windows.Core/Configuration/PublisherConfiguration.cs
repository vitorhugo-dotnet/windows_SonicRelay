namespace SonicRelay.Windows.Core.Configuration;

public sealed record PublisherConfiguration(
    Uri BackendBaseUrl,
    Uri SignalingBaseUrl,
    int DefaultMaxViewers,
    bool DevelopmentMode = false)
{
    public void Validate()
    {
        ValidateUrl(BackendBaseUrl, nameof(BackendBaseUrl), "http", "https");
        ValidateUrl(SignalingBaseUrl, nameof(SignalingBaseUrl), "ws", "wss", "http", "https");

        if (DefaultMaxViewers <= 0)
        {
            throw new ConfigurationValidationException("DefaultMaxViewers must be greater than zero.");
        }
    }

    private static void ValidateUrl(Uri? value, string name, params string[] schemes)
    {
        if (value is null || !value.IsAbsoluteUri || !schemes.Contains(value.Scheme, StringComparer.OrdinalIgnoreCase))
        {
            throw new ConfigurationValidationException($"{name} must be an absolute HTTP(S) or WebSocket URL.");
        }
    }
}

public sealed class ConfigurationValidationException(string message, Exception? innerException = null)
    : Exception(message, innerException);

