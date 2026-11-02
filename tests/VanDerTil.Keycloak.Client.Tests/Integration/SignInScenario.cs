using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

internal sealed class SignInScenario
{
    internal const string ClientId = "sample-app-web";
    private const string SeedUsername = "test-user";
    private const string ClientSecret = "integration-client-secret";
    private const string RedirectUri = "https://localhost:7257/signin-oidc";
    private readonly KeycloakFixture _fixture;

    private SignInScenario(KeycloakFixture fixture, string realmId)
    {
        _fixture = fixture;
        RealmId = realmId;
    }

    internal string RealmId { get; }

    internal static Task<SignInScenario> CreateRoleClaimsAsync(KeycloakFixture fixture, Action<UserBuilder> configureUser)
    {
        return CreateAsync(fixture,
            configureRealm: realm =>
            {
                realm.AddRole("UnrelatedRealm");
                realm.AddClient("other", client => client.AddRole("UnrelatedClient"));
                realm.AddGroup("Developer", group => group.WithClientRoles(ClientId, "View", "Manage"));
            },
            configureClient: client =>
            {
                client.WithFullScopeAllowed(false);
                client.AddRole("View").AddRole("Manage");
                client.WithClientRoleScope(ClientId, "View", "Manage");
                client.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("roles", "roles")
                    .WithConfig("usermodel.clientRoleMapping.clientId", ClientId));
            },
            configureUser: user =>
            {
                user.InRole("UnrelatedRealm").InClientRole("other", "UnrelatedClient");
                configureUser(user);
            });
    }

    internal static async Task<SignInScenario> CreateAsync(
        KeycloakFixture fixture,
        Action<RealmBuilder>? configureRealm = null,
        Action<ClientBuilder>? configureClient = null,
        Action<UserBuilder>? configureUser = null)
    {
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await fixture.ConfigureRealmAsync(realmId, realm =>
        {
            realm.Enabled();
            configureRealm?.Invoke(realm);
            realm.AddClient(ClientId, client =>
            {
                client.Confidential().WithSecret(ClientSecret).WithStandardFlow();
                client.WithRedirectUri(RedirectUri).WithPkceS256().WithRefreshTokens(true);
                configureClient?.Invoke(client);
            });
            realm.AddUser(SeedUsername, user =>
            {
                user.WithPassword("test-password").WithEmail("test-user@example.com");
                user.WithFirstName("Test").WithLastName("User");
                configureUser?.Invoke(user);
            });
        });
        return new SignInScenario(fixture, realmId);
    }

    internal async Task<HttpResponseMessage> RequestRefreshAsync(string refreshToken)
    {
        using var http = new HttpClient { BaseAddress = _fixture.BaseAddress };
        using var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["refresh_token"] = refreshToken,
        });
        return await http.PostAsync($"realms/{RealmId}/protocol/openid-connect/token", form);
    }

    internal async Task<JsonDocument> RefreshTokensAsync(string refreshToken)
    {
        using var response = await RequestRefreshAsync(refreshToken);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    internal async Task<JsonDocument> GetUserInfoAsync(string accessToken)
    {
        using var http = new HttpClient { BaseAddress = _fixture.BaseAddress };
        using var request = new HttpRequestMessage(HttpMethod.Get, $"realms/{RealmId}/protocol/openid-connect/userinfo");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        using var response = await http.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    // Exercise the actual authorization-code flow with S256, keeping the browser redirect local to the test.
    internal async Task<JsonDocument> LoginAsync()
    {
        using var handler = new HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new HttpClient(handler) { BaseAddress = _fixture.BaseAddress };
        var verifier = new string('a', 64);
        static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Base64Url(SHA256.HashData(Encoding.ASCII.GetBytes(verifier)));
        var auth = $"realms/{RealmId}/protocol/openid-connect/auth?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&scope=openid&state=integration&nonce=integration&code_challenge_method=S256&code_challenge={challenge}";
        using var authResponse = await http.GetAsync(auth);
        authResponse.EnsureSuccessStatusCode();
        var html = await authResponse.Content.ReadAsStringAsync();
        // Browsers treat localhost as trustworthy for Secure cookies; HttpClient does not.
        // Return this local test server's cookies explicitly when posting its login form.
        var cookies = authResponse.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]);
        http.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
        var action = Regex.Match(html, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value;
        action.ShouldNotBeNullOrWhiteSpace();
        var actionUri = new Uri(WebUtility.HtmlDecode(action));
        http.BaseAddress!.IsLoopback.ShouldBeTrue();
        actionUri.GetLeftPart(UriPartial.Authority).ShouldBe(http.BaseAddress.GetLeftPart(UriPartial.Authority));
        using var login = await http.PostAsync(actionUri, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = SeedUsername,
            ["password"] = "test-password",
            ["credentialId"] = "",
        }));
        var loginHtml = await login.Content.ReadAsStringAsync();
        var loginError = Regex.Match(loginHtml, "<p[^>]*class=\"[^\"]*instruction[^\"]*\"[^>]*>([^<]*)").Groups[1].Value;
        login.StatusCode.ShouldBe(HttpStatusCode.Found, loginError);
        var query = login.Headers.Location!.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        query.ContainsKey("code").ShouldBeTrue();
        using var response = await http.PostAsync($"realms/{RealmId}/protocol/openid-connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = RedirectUri,
            ["code"] = query["code"],
            ["code_verifier"] = verifier,
        }));
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    internal static string[] ReadRoles(string token)
    {
        using var json = ReadTokenPayload(token);
        return json.RootElement.TryGetProperty("roles", out var roles) ? roles.EnumerateArray().Select(r => r.GetString()!).Order().ToArray() : [];
    }

    internal static JsonDocument ReadTokenPayload(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        return JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
    }
}
