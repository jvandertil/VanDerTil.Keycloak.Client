using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace VanDerTil.Keycloak.Client;

/// <summary>
/// Requests OpenID Connect access tokens from Keycloak for administrative API calls.
/// Supports multiple authentication modes including direct grant, client credentials, and external token providers.
/// </summary>
internal sealed class OpenIdConnectAccessTokenProvider
{
    /// <summary>
    /// The name of the HTTP client registration used for token requests.
    /// </summary>
    public const string HttpClientName = "KeycloakToken";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly KeycloakConnectionOptions _options;
    private readonly Uri _tokenEndpoint;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenIdConnectAccessTokenProvider"/> class.
    /// </summary>
    /// <param name="httpClientFactory">The factory used to create the HTTP client that calls the Keycloak token endpoint.</param>
    /// <param name="options">The configured Keycloak connection options that identify the token endpoint and authentication method.</param>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="httpClientFactory"/> or <paramref name="options"/> is <see langword="null"/>.
    /// </exception>
    public OpenIdConnectAccessTokenProvider(IHttpClientFactory httpClientFactory, IOptions<KeycloakConnectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientFactory = httpClientFactory;
        _options = options.Value;

        _tokenEndpoint = new Uri(
            new Uri(_options.BaseUrl, UriKind.Absolute),
            $"/realms/{_options.AdminRealm}/protocol/openid-connect/token");
    }

    /// <summary>
    /// Acquires a bearer token from Keycloak using the configured authentication mode.
    /// </summary>
    /// <param name="cancellationToken">A token that can be used to cancel the HTTP request.</param>
    /// <returns>A task that returns the access token string.</returns>
    /// <exception cref="HttpRequestException">Thrown when the token endpoint returns a non-success status code.</exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the authentication mode is not properly configured, the token endpoint response is empty,
    /// or the response does not contain an access token.
    /// </exception>
    /// <remarks>
    /// The authentication method is determined by <see cref="KeycloakConnectionOptions.AuthenticationMode"/>.
    /// Each mode requires different configuration properties to be set.
    /// </remarks>
    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken)
    {
        return _options.AuthenticationMode switch
        {
            AuthenticationMode.DirectGrant => await GetTokenViaDirectGrantAsync(cancellationToken).ConfigureAwait(false),
            AuthenticationMode.ClientCredentials => await GetTokenViaClientCredentialsAsync(cancellationToken).ConfigureAwait(false),
            AuthenticationMode.ExternalTokenProvider => await GetTokenViaExternalProviderAsync(cancellationToken).ConfigureAwait(false),
            _ => throw new InvalidOperationException($"Unsupported authentication mode: {_options.AuthenticationMode}")
        };
    }

    private async Task<string> GetTokenViaDirectGrantAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.AdminUsername))
        {
            throw new InvalidOperationException(
                "AdminUsername is required when using DirectGrant authentication mode.");
        }

        if (string.IsNullOrWhiteSpace(_options.AdminPassword))
        {
            throw new InvalidOperationException(
                "AdminPassword is required when using DirectGrant authentication mode.");
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", "admin-cli" },
                { "username", _options.AdminUsername },
                { "password", _options.AdminPassword }
            })
        };

        return await SendTokenRequestAsync(httpClient, tokenRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenViaClientCredentialsAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ClientId))
        {
            throw new InvalidOperationException(
                "ClientId is required when using ClientCredentials authentication mode.");
        }

        if (string.IsNullOrWhiteSpace(_options.ClientSecret))
        {
            throw new InvalidOperationException(
                "ClientSecret is required when using ClientCredentials authentication mode.");
        }

        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "client_credentials" },
                { "client_id", _options.ClientId },
                { "client_secret", _options.ClientSecret }
            })
        };

        return await SendTokenRequestAsync(httpClient, tokenRequest, cancellationToken).ConfigureAwait(false);
    }

    private async Task<string> GetTokenViaExternalProviderAsync(CancellationToken cancellationToken)
    {
        if (_options.TokenProvider is null)
        {
            throw new InvalidOperationException(
                "TokenProvider is required when using ExternalTokenProvider authentication mode.");
        }

        var token = await _options.TokenProvider(cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException(
                "The external token provider returned a null or empty access token.");
        }

        return token;
    }

    private static async Task<string> SendTokenRequestAsync(
        HttpClient httpClient,
        HttpRequestMessage tokenRequest,
        CancellationToken cancellationToken)
    {
        using var response = await httpClient.SendAsync(tokenRequest, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var tokenResponse = await response.Content.ReadFromJsonAsync<AccessTokenResponse>(cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException("The token endpoint returned an empty response.");

        if (string.IsNullOrWhiteSpace(tokenResponse.AccessToken))
        {
            throw new InvalidOperationException("The token endpoint response did not contain an access token.");
        }

        return tokenResponse.AccessToken;
    }

    private sealed class AccessTokenResponse
    {
        [JsonPropertyName("access_token")]
        public string AccessToken { get; set; } = string.Empty;
    }
}
