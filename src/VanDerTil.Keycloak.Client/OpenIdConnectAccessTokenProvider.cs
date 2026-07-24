using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json.Serialization;

using Microsoft.Extensions.Options;

namespace VanDerTil.Keycloak.Client;

public sealed class OpenIdConnectAccessTokenProvider
{
    public const string HttpClientName = "KeycloakToken";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly Uri _tokenEndpoint;
    private readonly string _username;
    private readonly string _password;

    public OpenIdConnectAccessTokenProvider(IHttpClientFactory httpClientFactory, IOptions<KeycloakConnectionOptions> options)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);
        ArgumentNullException.ThrowIfNull(options);

        _httpClientFactory = httpClientFactory;

        var bootstrapOptions = options.Value;
        _tokenEndpoint = new Uri(new Uri(bootstrapOptions.BaseUrl, UriKind.Absolute), $"/realms/{bootstrapOptions.AdminRealm}/protocol/openid-connect/token");
        _username = bootstrapOptions.AdminUsername;
        _password = bootstrapOptions.AdminPassword;
    }

    public async Task<string> GetAuthorizationTokenAsync(CancellationToken cancellationToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);

        using var tokenRequest = new HttpRequestMessage(HttpMethod.Post, _tokenEndpoint)
        {
            Content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                { "grant_type", "password" },
                { "client_id", "admin-cli" },
                { "username", _username },
                { "password", _password }
            })
        };

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
