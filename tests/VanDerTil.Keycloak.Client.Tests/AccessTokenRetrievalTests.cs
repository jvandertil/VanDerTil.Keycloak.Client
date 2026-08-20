using System.Net;
using System.Net.Http;
using System.Text;

using Microsoft.Extensions.Options;

namespace VanDerTil.Keycloak.Client.Tests;

public sealed class AccessTokenRetrievalTests
{
    [TestClass]
    public sealed class ObtainingAnAccessToken
    {
        [TestMethod]
        public async Task Should_ThrowInvalidOperationException_When_AccessTokenIsMissing()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{}", Encoding.UTF8, "application/json"),
                });


            var provider = CreateProvider(handler);

            await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAccessTokenAsync(CancellationToken.None));
        }

        [TestMethod]
        public async Task Should_ThrowInvalidOperationException_When_AccessTokenIsWhitespace()
        {
            var handler = new StubHttpMessageHandler(_ =>
                new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"access_token\":\"   \"}", Encoding.UTF8, "application/json"),
                });

            var provider = CreateProvider(handler);

            await Should.ThrowAsync<InvalidOperationException>(() => provider.GetAccessTokenAsync(CancellationToken.None));
        }

        private static OpenIdConnectAccessTokenProvider CreateProvider(HttpMessageHandler handler)
        {
            var options = Options.Create(new KeycloakConnectionOptions
            {
                BaseUrl = "http://localhost:43000",
                AuthenticationMode = AuthenticationMode.DirectGrant,
                AdminUsername = "admin",
                AdminPassword = "admin",
                AdminRealm = "master",
            });

            var factory = new StubHttpClientFactory(new HttpClient(handler));
            return new OpenIdConnectAccessTokenProvider(factory, options);
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

        private sealed class StubHttpMessageHandler : HttpMessageHandler
        {
            private readonly Func<HttpRequestMessage, HttpResponseMessage> _responseFactory;

            public StubHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> responseFactory)
            {
                _responseFactory = responseFactory;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(_responseFactory(request));
            }
        }
    }
}
