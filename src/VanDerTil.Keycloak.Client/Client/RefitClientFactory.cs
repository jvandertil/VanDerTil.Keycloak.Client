using System.Net.Http.Headers;

using Refit;

namespace VanDerTil.Keycloak.Client.Client;

public sealed class RefitClientFactory
{
    public const string HttpClientName = "KeycloakAdminApi";

    private readonly IHttpClientFactory _httpClientFactory;

    public RefitClientFactory(IHttpClientFactory httpClientFactory)
    {
        ArgumentNullException.ThrowIfNull(httpClientFactory);

        _httpClientFactory = httpClientFactory;
    }

    public TClient Create<TClient>(string accessToken)
    {
        var httpClient = _httpClientFactory.CreateClient(HttpClientName);
        httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return RestService.For<TClient>(httpClient);
    }
}
