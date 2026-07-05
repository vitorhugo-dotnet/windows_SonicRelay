using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using SonicRelay.Windows.ApiClient.Authentication;
using SonicRelay.Windows.ApiClient.Errors;
using SonicRelay.Windows.Core.Storage;

namespace SonicRelay.Windows.ApiClient;

internal sealed class ApiHttpClient(HttpClient httpClient, ITokenStore tokenStore)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<TResponse> SendAsync<TResponse>(
        HttpMethod method,
        string path,
        object? body,
        bool authenticated,
        CancellationToken cancellationToken,
        bool allowRefresh = true)
    {
        var tokens = authenticated ? await LoadTokensAsync(cancellationToken) : null;
        using var response = await SendOnceAsync(method, path, body, tokens?.AccessToken, cancellationToken);

        if (response.StatusCode == HttpStatusCode.Unauthorized
            && authenticated
            && allowRefresh
            && !string.IsNullOrWhiteSpace(tokens?.RefreshToken))
        {
            var refreshed = await RefreshTokensAsync(tokens.RefreshToken, cancellationToken);
            using var retry = await SendOnceAsync(method, path, body, refreshed.AccessToken, cancellationToken);
            return await ReadAsync<TResponse>(retry, cancellationToken);
        }

        return await ReadAsync<TResponse>(response, cancellationToken);
    }

    public async Task<TokenSet> RefreshTokensAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var response = await SendAsync<IdentityTokenResponse>(
            HttpMethod.Post,
            "/auth/refresh",
            new RefreshTokenRequest(refreshToken),
            authenticated: false,
            cancellationToken,
            allowRefresh: false);
        var tokens = response.ToTokenSet();
        await SaveTokensAsync(tokens, cancellationToken);
        return tokens;
    }

    public Task SaveTokensAsync(TokenSet tokens, CancellationToken cancellationToken) =>
        SaveTokensCoreAsync(tokens, cancellationToken);

    private async Task<HttpResponseMessage> SendOnceAsync(
        HttpMethod method,
        string path,
        object? body,
        string? accessToken,
        CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(method, path);
        if (body is not null)
        {
            request.Content = JsonContent.Create(body, options: JsonOptions);
        }
        if (!string.IsNullOrWhiteSpace(accessToken))
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        }

        try
        {
            return await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        }
        catch (TaskCanceledException exception) when (!cancellationToken.IsCancellationRequested)
        {
            throw new ApiClientException(ApiErrorKind.BackendUnavailable, "The backend request timed out.", innerException: exception);
        }
        catch (HttpRequestException exception)
        {
            throw new ApiClientException(ApiErrorKind.NetworkUnavailable, "The backend network is unavailable.", innerException: exception);
        }
    }

    private static async Task<TResponse> ReadAsync<TResponse>(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw await CreateErrorAsync(response, cancellationToken);
        }

        try
        {
            var value = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
            return value ?? throw new ApiClientException(ApiErrorKind.Unknown, "The backend returned an empty response.");
        }
        catch (JsonException exception)
        {
            throw new ApiClientException(ApiErrorKind.Unknown, "The backend returned an invalid JSON response.", innerException: exception);
        }
    }

    private static async Task<ApiClientException> CreateErrorAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var detail = await ReadErrorDetailAsync(response, cancellationToken);
        var kind = response.StatusCode switch
        {
            HttpStatusCode.Unauthorized => ApiErrorKind.Unauthorized,
            HttpStatusCode.Forbidden => ApiErrorKind.Forbidden,
            HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity => ApiErrorKind.Validation,
            HttpStatusCode.Conflict => ApiErrorKind.Conflict,
            >= HttpStatusCode.InternalServerError => ApiErrorKind.BackendUnavailable,
            _ => ApiErrorKind.Unknown
        };
        return new ApiClientException(kind, detail ?? $"Backend request failed with status {(int)response.StatusCode}.", response.StatusCode);
    }

    private static async Task<string?> ReadErrorDetailAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        try
        {
            using var document = await JsonDocument.ParseAsync(
                await response.Content.ReadAsStreamAsync(cancellationToken),
                cancellationToken: cancellationToken);
            foreach (var property in new[] { "error", "detail", "title" })
            {
                if (document.RootElement.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String)
                {
                    return value.GetString();
                }
            }
        }
        catch (JsonException)
        {
        }
        return null;
    }

    private async Task<TokenSet?> LoadTokensAsync(CancellationToken cancellationToken)
    {
        var result = await tokenStore.LoadAsync(cancellationToken);
        if (!result.Succeeded)
        {
            throw new ApiClientException(ApiErrorKind.Unknown, result.Message ?? "Authentication tokens could not be loaded.");
        }
        return result.Tokens;
    }

    private async Task SaveTokensCoreAsync(TokenSet tokens, CancellationToken cancellationToken)
    {
        var result = await tokenStore.SaveAsync(tokens, cancellationToken);
        if (!result.Succeeded)
        {
            throw new ApiClientException(ApiErrorKind.Unknown, result.Message ?? "Authentication tokens could not be saved.");
        }
    }
}
