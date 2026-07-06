using System.Text.Json;

namespace SonicRelay.Windows.Core.Configuration;

public sealed class UserConfigurationLoader
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    public static string DefaultDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "SonicRelay",
        "WindowsPublisher");

    public static string DefaultPath => Path.Combine(DefaultDirectory, "appsettings.json");

    private readonly string _path;

    public UserConfigurationLoader(string? path = null) => _path = path ?? DefaultPath;

    public async Task<PublisherConfiguration> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(_path))
        {
            Directory.CreateDirectory(Path.GetDirectoryName(_path)!);
            var template = new ConfigurationDocument("https://localhost:5001/", "wss://localhost:5001/ws/signaling", 4, false);
            await File.WriteAllTextAsync(_path, JsonSerializer.Serialize(template, JsonOptions), cancellationToken);
        }

        try
        {
            await using var stream = File.OpenRead(_path);
            var document = await JsonSerializer.DeserializeAsync<ConfigurationDocument>(stream, JsonOptions, cancellationToken)
                ?? throw new ConfigurationValidationException("Configuration file is empty.");
            var configuration = new PublisherConfiguration(
                ParseUri(document.BackendBaseUrl, "BackendBaseUrl"),
                ParseUri(document.SignalingBaseUrl, "SignalingBaseUrl"),
                document.DefaultMaxViewers,
                document.DevelopmentMode);
            configuration.Validate();
            return configuration;
        }
        catch (ConfigurationValidationException)
        {
            throw;
        }
        catch (JsonException exception)
        {
            throw new ConfigurationValidationException("Configuration file contains invalid JSON.", exception);
        }
    }

    private static Uri ParseUri(string? value, string name)
    {
        if (!Uri.TryCreate(value, UriKind.Absolute, out var uri))
        {
            throw new ConfigurationValidationException($"{name} must be an absolute URL.");
        }

        return uri;
    }

    private sealed record ConfigurationDocument(
        string? BackendBaseUrl,
        string? SignalingBaseUrl,
        int DefaultMaxViewers,
        bool DevelopmentMode);
}

