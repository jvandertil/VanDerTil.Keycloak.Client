using System.Net;
using System.Net.Http;
using System.Text;

using Microsoft.Extensions.Options;

namespace VanDerTil.Keycloak.Client.Tests;

[TestClass]
public sealed class AccessTokenRetrievalTests
{
    [TestMethod]
    [DataRow("{}")]
    [DataRow("{\"access_token\":\"   \"}")]
    public async Task Should_RejectTokenResponseWithoutUsableAccessToken(string responseJson)
    {
        // Arrange
        using var httpClient = new HttpClient(new StubTokenResponseHandler(responseJson));
        var provider = CreateProvider(httpClient);

        // Act
        Func<Task> requestToken = () => provider.GetAccessTokenAsync(CancellationToken.None);

        // Assert
        await Should.ThrowAsync<InvalidOperationException>(requestToken);
    }

    private static OpenIdConnectAccessTokenProvider CreateProvider(HttpClient httpClient)
    {
        var options = Options.Create(new KeycloakConnectionOptions
        {
            BaseUrl = "http://localhost:43000",
            AuthenticationMode = AuthenticationMode.DirectGrant,
            AdminUsername = "admin",
            AdminPassword = "admin",
            AdminRealm = "master",
        });

        return new OpenIdConnectAccessTokenProvider(new StubHttpClientFactory(httpClient), options);
    }

    private sealed class StubHttpClientFactory : IHttpClientFactory
    {
        private readonly HttpClient _httpClient;

        public StubHttpClientFactory(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public HttpClient CreateClient(string name)
        {
            return _httpClient;
        }
    }

    private sealed class StubTokenResponseHandler : HttpMessageHandler
    {
        private readonly string _responseJson;

        public StubTokenResponseHandler(string responseJson)
        {
            _responseJson = responseJson;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_responseJson, Encoding.UTF8, "application/json"),
            });
        }
    }
}
