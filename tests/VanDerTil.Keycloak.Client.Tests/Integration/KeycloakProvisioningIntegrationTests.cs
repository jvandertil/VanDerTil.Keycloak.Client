using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Testcontainers.Keycloak;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class KeycloakProvisioningIntegrationTests
{
    private const string AdminUsername = "admin";
    private const string AdminPassword = "admin";
    private const string ClientId = "sample-app-web";
    private const string SeedUsername = "test-user";
    private const string RedirectUri = "https://localhost:7257/signin-oidc";
    private const string PostLogoutRedirectUri = "https://localhost:7257/signout-callback-oidc";
    private const string ClientSecret = "integration-client-secret";
    private const string TemporaryPassword = "temporary-test-password";

    private static KeycloakContainer? _container;
    private static ServiceProvider? _serviceProvider;
    private static KeycloakConnectionOptions? _options;

    [ClassInitialize]
    public static async Task ClassInitializeAsync(TestContext testContext)
    {
        // This should be kept in sync with the Keycloak version used in the AppHost project.
        _container = new KeycloakBuilder("quay.io/keycloak/keycloak:26.7")
            .WithUsername(AdminUsername)
            .WithPassword(AdminPassword)
            .Build();

        await _container.StartAsync().ConfigureAwait(false);

        _options = new KeycloakConnectionOptions
        {
            BaseUrl = _container.GetBaseAddress().ToString(),
            AuthenticationMode = AuthenticationMode.DirectGrant,
            AdminUsername = AdminUsername,
            AdminPassword = AdminPassword,
            AdminRealm = "master",
            SeedTestUser = true,
        };

        var services = new ServiceCollection();

        services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddOptions<KeycloakConnectionOptions>()
            .Configure(options =>
            {
                options.BaseUrl = _options.BaseUrl;
                options.AuthenticationMode = _options.AuthenticationMode;
                options.AdminUsername = _options.AdminUsername;
                options.AdminPassword = _options.AdminPassword;
                options.SeedTestUser = _options.SeedTestUser;
                options.AdminRealm = _options.AdminRealm;
            });

        services.AddHttpClient(OpenIdConnectAccessTokenProvider.HttpClientName, static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient(RefitClientFactory.HttpClientName, static (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KeycloakConnectionOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<OpenIdConnectAccessTokenProvider>();
        services.AddSingleton<RefitClientFactory>();
        services.AddSingleton<KeycloakConfigurator>();

        _serviceProvider = services.BuildServiceProvider();
    }

    [ClassCleanup]
    public static async Task ClassCleanupAsync()
    {
        if (_serviceProvider is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
        }
        else
        {
            _serviceProvider?.Dispose();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync().ConfigureAwait(false);
        }
    }

    [TestMethod]
    public async Task Should_ProvisionRealmClientRolesAndUser()
    {
        EnsureIntegrationReady();
        var realmId = CreateUniqueRealmId();

        await ConfigureKeycloakAsync(realmId).ConfigureAwait(false);

        var realms = await (await CreateClientAsync<IRealmsResource>()).GetAllAsync(CancellationToken.None).ConfigureAwait(false);
        var realm = realms.SingleOrDefault(r => string.Equals(r.Realm, realmId, StringComparison.OrdinalIgnoreCase));

        realm.ShouldNotBeNull();
        realm.Enabled.ShouldBeTrue();

        var roles = await (await CreateClientAsync<IRolesResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        roles.Select(role => role.Name).ShouldContain("admin");
        roles.Select(role => role.Name).ShouldContain("scheduler");

        var clients = await (await CreateClientAsync<IClientsResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        var webClient = clients.SingleOrDefault(client => string.Equals(client.ClientId, ClientId, StringComparison.Ordinal));

        webClient.ShouldNotBeNull();
        webClient.PublicClient.ShouldBeFalse();
        webClient.StandardFlowEnabled.ShouldBeTrue();
        webClient.RedirectUris.ShouldNotBeNull();
        webClient.RedirectUris.ShouldContain(RedirectUri);
        webClient.Attributes.ShouldNotBeNull();
        webClient.Attributes!["post.logout.redirect.uris"].Split("##").ShouldContain(PostLogoutRedirectUri);

        webClient.Id.ShouldNotBeNullOrWhiteSpace();
        var clientInternalId = webClient.Id!;

        var clientRoles = await (await CreateClientAsync<IClientRolesResource>())
            .GetAllAsync(realmId, clientInternalId, CancellationToken.None)
            .ConfigureAwait(false);

        clientRoles.Select(r => r.Name).ShouldContain("admin");
        clientRoles.Select(r => r.Name).ShouldContain("scheduler");

        var protocolMappers = await (await CreateClientAsync<IClientProtocolMappersResource>())
            .GetAllAsync(realmId, clientInternalId, CancellationToken.None)
            .ConfigureAwait(false);

        var rolesMapper = protocolMappers.SingleOrDefault(m => string.Equals(m.Name, "client-roles", StringComparison.Ordinal));
        rolesMapper.ShouldNotBeNull();
        rolesMapper.ProtocolMapper.ShouldBe("oidc-usermodel-client-role-mapper");
        rolesMapper.Config.ShouldNotBeNull();
        rolesMapper.Config["claim.name"].ShouldBe("roles");

        var users = await (await CreateClientAsync<IUsersResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        var testUser = users.SingleOrDefault(user => string.Equals(user.Username, SeedUsername, StringComparison.Ordinal));

        testUser.ShouldNotBeNull();
        testUser.Id.ShouldNotBeNullOrWhiteSpace();

        var userClientRoles = await (await CreateClientAsync<IUsersResource>())
            .GetClientRoleMappingsAsync(realmId, testUser.Id, clientInternalId, CancellationToken.None)
            .ConfigureAwait(false);

        userClientRoles.Select(role => role.Name).ShouldContain("admin");
    }

    [TestMethod]
    public async Task Should_BeIdempotent_When_CalledMultipleTimes()
    {
        EnsureIntegrationReady();
        var realmId = CreateUniqueRealmId();

        await ConfigureKeycloakAsync(realmId).ConfigureAwait(false);
        await ConfigureKeycloakAsync(realmId).ConfigureAwait(false);

        var roles = await (await CreateClientAsync<IRolesResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        roles.Count(role => string.Equals(role.Name, "admin", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);
        roles.Count(role => string.Equals(role.Name, "scheduler", StringComparison.OrdinalIgnoreCase)).ShouldBe(1);

        var users = await (await CreateClientAsync<IUsersResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        users.Count(user => string.Equals(user.Username, SeedUsername, StringComparison.Ordinal)).ShouldBe(1);

        var clients = await (await CreateClientAsync<IClientsResource>()).GetAllAsync(realmId, CancellationToken.None).ConfigureAwait(false);
        clients.Count(client => string.Equals(client.ClientId, ClientId, StringComparison.Ordinal)).ShouldBe(1);
    }

    [TestMethod]
    public async Task Should_ReconcileManagedGroupsAndScopesAndPreserveUnrelatedState()
    {
        var realmId = CreateUniqueRealmId();
        var configurator = _serviceProvider!.GetRequiredService<KeycloakConfigurator>();
        async Task ConfigureAsync(string[] permissions, bool includeMappings = true, bool includeUnrelated = false) => await configurator.ConfigureAsync(builder =>
        {
            builder.AddRealm(realmId, realm =>
            {
                realm.Enabled().WithRefreshTokenRotation(true)
                    .WithSsoSessionIdleTimeout(TimeSpan.FromMinutes(40))
                    .WithSsoSessionMaxLifespan(TimeSpan.FromHours(9));
                realm.AddClient("other", c => c.AddRole("Unrelated"));
                realm.AddClient(ClientId, c =>
                {
                    c.Confidential().WithSecret(ClientSecret).WithStandardFlow().WithRedirectUri(RedirectUri).Enabled()
                        .WithFullScopeAllowed(false).WithPkceS256().WithRefreshTokens(true)
                        .WithAccessTokenLifespan(TimeSpan.FromMinutes(5))
                        .WithClientSessionIdleTimeout(TimeSpan.FromMinutes(35))
                        .WithClientSessionMaxLifespan(TimeSpan.FromHours(8));
                    c.AddRole("View").AddRole("Manage");
                    if (includeMappings) c.WithClientRoleScope(ClientId, permissions);
                    if (includeUnrelated) c.WithClientRoleScope("other", "Unrelated");
                });
                realm.AddGroup("Developer", g =>
                {
                    if (includeMappings) g.WithClientRoles(ClientId, permissions);
                    if (includeUnrelated) g.WithClientRoles("other", "Unrelated");
                });
                realm.AddGroup("Other", _ => { });
                realm.AddUser("member", u =>
                {
                    u.InGroup("Developer");
                    if (includeUnrelated) u.InGroup("Other");
                });
                if (includeUnrelated) realm.AddUser("unrelated-user", u => u.WithFirstName("Preserved"));
            });
        }, CancellationToken.None);

        // Fresh setup and repeated setup produce one group and membership, with exact mappings.
        await configurator.ConfigureAsync(b => b.AddRealm(realmId, r => r.Enabled().AddGroup("Parent", _ => { })), CancellationToken.None);
        var groupResource = await CreateClientAsync<IGroupsResource>();
        using var parent = await groupResource.GetByPathAsync(realmId, "Parent", CancellationToken.None);
        using var nested = await groupResource.CreateChildAsync(realmId, parent.Content!.Id!, new Client.Representations.GroupRepresentation { Name = "Developer" }, CancellationToken.None);
        nested.IsSuccessStatusCode.ShouldBeTrue();
        await ConfigureAsync(["View", "Manage"], includeUnrelated: true);
        await ConfigureAsync(["View", "Manage"]);
        var groups = await CreateClientAsync<IGroupsResource>();
        var group = (await groups.FindAsync(realmId, "Developer", CancellationToken.None)).Single(g => g.Name == "Developer");
        var clients = await (await CreateClientAsync<IClientsResource>()).GetAllAsync(realmId);
        var web = clients.Single(c => c.ClientId == ClientId);
        var other = clients.Single(c => c.ClientId == "other");
        async Task<string?[]> GroupRolesAsync(string clientId) => (await groups.GetRolesAsync(realmId, group.Id!, clientId, CancellationToken.None)).Select(r => r.Name).Order().ToArray();
        var scopes = await CreateClientAsync<IClientScopeMappingsResource>();
        async Task<string?[]> ScopeRolesAsync() => (await scopes.GetAsync(realmId, web.Id!, web.Id!, CancellationToken.None)).Select(r => r.Name).Order().ToArray();
        (await GroupRolesAsync(web.Id!)).ShouldBe(new[] { "Manage", "View" });
        (await ScopeRolesAsync()).ShouldBe(new[] { "Manage", "View" });

        // Catalogue changes remove obsolete managed mappings, not roles or other-client grants.
        await ConfigureAsync(["View"]);
        (await GroupRolesAsync(web.Id!)).ShouldBe(new[] { "View" });
        (await ScopeRolesAsync()).ShouldBe(new[] { "View" });
        (await GroupRolesAsync(other.Id!)).ShouldBe(new[] { "Unrelated" });
        await Should.ThrowAsync<InvalidOperationException>(() => ConfigureAsync(["MissingPermission"]));
        (await GroupRolesAsync(web.Id!)).ShouldBe(new[] { "View" });
        (await ScopeRolesAsync()).ShouldBe(new[] { "View" });
        await ConfigureAsync([], includeMappings: false);
        (await GroupRolesAsync(web.Id!)).ShouldBe(new[] { "View" });
        (await ScopeRolesAsync()).ShouldBe(new[] { "View" });
        await ConfigureAsync([]);
        (await GroupRolesAsync(web.Id!)).ShouldBeEmpty();
        (await ScopeRolesAsync()).ShouldBeEmpty();
        (await scopes.GetAsync(realmId, web.Id!, other.Id!, CancellationToken.None)).Select(r => r.Name).ShouldBe(new[] { "Unrelated" });

        // Omitted settings survive partial updates, including unmodeled Keycloak settings.
        web.FullScopeAllowed.ShouldBe(false);
        web.Attributes!["pkce.code.challenge.method"].ShouldBe("S256");
        web.Attributes["access.token.lifespan"].ShouldBe("300");
        web.Attributes["client.session.idle.timeout"].ShouldBe("2100");
        web.Attributes["client.session.max.lifespan"].ShouldBe("28800");
        web.Attributes["use.refresh.tokens"].ShouldBe("true");
        web.AdditionalProperties!["directAccessGrantsEnabled"] = System.Text.Json.JsonSerializer.SerializeToElement(true);
        await (await CreateClientAsync<IClientsResource>()).UpdateAsync(realmId, web.Id!, web);
        await configurator.ConfigureAsync(b => b.AddRealm(realmId, r => r.AddClient(ClientId, c => c.WithName("Renamed"))), CancellationToken.None);
        web = (await (await CreateClientAsync<IClientsResource>()).GetAllAsync(realmId)).Single(c => c.ClientId == ClientId);
        web.FullScopeAllowed.ShouldBe(false);
        web.Attributes!["pkce.code.challenge.method"].ShouldBe("S256");
        web.AdditionalProperties!["directAccessGrantsEnabled"].GetBoolean().ShouldBeTrue();
        var realmSettings = await (await CreateClientAsync<IRealmResource>()).GetAsync(realmId);
        realmSettings.RevokeRefreshToken.ShouldBe(true);
        realmSettings.RefreshTokenMaxReuse.ShouldBe(0);
        realmSettings.SsoSessionIdleTimeout.ShouldBe(2400);
        realmSettings.SsoSessionMaxLifespan.ShouldBe(32400);
        await configurator.ConfigureAsync(b => b.AddRealm(realmId, r =>
        {
            r.WithRefreshTokenRotation(false, 2).WithSsoSessionIdleTimeout(TimeSpan.FromMinutes(50));
            r.AddClient(ClientId, c => c.WithFullScopeAllowed(true).WithRefreshTokens(false).WithAccessTokenLifespan(TimeSpan.FromMinutes(2)));
        }), CancellationToken.None);
        web = (await (await CreateClientAsync<IClientsResource>()).GetAllAsync(realmId)).Single(c => c.ClientId == ClientId);
        web.FullScopeAllowed.ShouldBe(true);
        web.Attributes!["use.refresh.tokens"].ShouldBe("false");
        web.Attributes["access.token.lifespan"].ShouldBe("120");
        realmSettings = await (await CreateClientAsync<IRealmResource>()).GetAsync(realmId);
        realmSettings.RevokeRefreshToken.ShouldBe(false);
        realmSettings.RefreshTokenMaxReuse.ShouldBe(2);
        realmSettings.SsoSessionIdleTimeout.ShouldBe(3000);
        var users = await (await CreateClientAsync<IUsersResource>()).GetAllAsync(realmId);
        users.Single(u => u.Username == "unrelated-user").FirstName.ShouldBe("Preserved");
        var member = users.Single(u => u.Username == "member");
        var memberships = await (await CreateClientAsync<IUsersResource>()).GetGroupsAsync(realmId, member.Id!, CancellationToken.None);
        memberships.Select(g => g.Name).Order().ShouldBe(new[] { "Developer", "Other" });
    }

    [TestMethod]
    public async Task Should_IssueRestrictedIdTokenRolesAndRotateSessionBoundRefreshTokens()
    {
        var realmId = CreateUniqueRealmId();
        var configurator = _serviceProvider!.GetRequiredService<KeycloakConfigurator>();
        await configurator.ConfigureAsync(b => b.AddRealm(realmId, realm =>
        {
            realm.Enabled().WithRefreshTokenRotation(true);
            realm.AddRole("UnrelatedRealm");
            realm.AddClient("other", c => c.AddRole("UnrelatedClient"));
            realm.AddClient(ClientId, c =>
            {
                c.Confidential().WithSecret(ClientSecret).WithStandardFlow().WithRedirectUri(RedirectUri)
                    .WithPkceS256().WithFullScopeAllowed(false).WithRefreshTokens(true)
                    .WithAccessTokenLifespan(TimeSpan.FromMinutes(5));
                c.AddRole("View").AddRole("Manage");
                c.WithClientRoleScope(ClientId, "View", "Manage");
                c.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("roles", "roles")
                    .WithConfig("usermodel.clientRoleMapping.clientId", ClientId));
            });
            realm.AddGroup("Developer", g => g.WithClientRoles(ClientId, "View", "Manage"));
            foreach (var name in new[] { "none", "limited", "developer" })
            {
                realm.AddUser(name, u =>
                {
                    u.WithPassword("test-password").WithEmail(name + "@example.com").WithFirstName("Test").WithLastName("User");
                    u.InRole("UnrelatedRealm").InClientRole("other", "UnrelatedClient");
                    if (name == "limited") u.InClientRole(ClientId, "View");
                    if (name == "developer") u.InGroup("Developer");
                });
            }
        }), CancellationToken.None);

        using var none = await LoginAsync(realmId, "none");
        ReadRoles(none.RootElement.GetProperty("id_token").GetString()!).ShouldBeEmpty();
        using var limited = await LoginAsync(realmId, "limited");
        ReadRoles(limited.RootElement.GetProperty("id_token").GetString()!).ShouldBe(new[] { "View" });
        using var developer = await LoginAsync(realmId, "developer");
        ReadRoles(developer.RootElement.GetProperty("id_token").GetString()!).ShouldBe(new[] { "Manage", "View" });
        developer.RootElement.GetProperty("expires_in").GetInt32().ShouldBe(300);
        var refreshToken = developer.RootElement.GetProperty("refresh_token").GetString()!;
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(_options!.BaseUrl) };
        async Task<System.Net.Http.HttpResponseMessage> RefreshAsync(string token) => await http.PostAsync(
            $"realms/{realmId}/protocol/openid-connect/token", new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["grant_type"] = "refresh_token",
                ["client_id"] = ClientId,
                ["client_secret"] = ClientSecret,
                ["refresh_token"] = token,
            }));
        await configurator.ConfigureAsync(b => b.AddRealm(realmId, r => r.AddGroup("Developer", g => g.WithClientRoles(ClientId, "View"))), CancellationToken.None);
        using var refreshed = await RefreshAsync(refreshToken);
        refreshed.EnsureSuccessStatusCode();
        using var renewed = System.Text.Json.JsonDocument.Parse(await refreshed.Content.ReadAsStringAsync());
        renewed.RootElement.GetProperty("refresh_token").GetString().ShouldNotBe(refreshToken);
        ReadRoles(renewed.RootElement.GetProperty("id_token").GetString()!).ShouldBe(new[] { "View" });
        using var userInfoRequest = new System.Net.Http.HttpRequestMessage(System.Net.Http.HttpMethod.Get, $"realms/{realmId}/protocol/openid-connect/userinfo");
        userInfoRequest.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", renewed.RootElement.GetProperty("access_token").GetString());
        using var userInfoResponse = await http.SendAsync(userInfoRequest);
        userInfoResponse.EnsureSuccessStatusCode();
        using var userInfo = System.Text.Json.JsonDocument.Parse(await userInfoResponse.Content.ReadAsStringAsync());
        userInfo.RootElement.GetProperty("roles").EnumerateArray().Select(r => r.GetString()).ShouldBe(new[] { "View" });
        using var replay = await RefreshAsync(refreshToken);
        replay.StatusCode.ShouldBe(System.Net.HttpStatusCode.BadRequest);
    }

    // Exercise the actual authorization-code flow with S256, keeping the browser redirect local to the test.
    private static async Task<System.Text.Json.JsonDocument> LoginAsync(string realmId, string username)
    {
        using var handler = new System.Net.Http.HttpClientHandler { AllowAutoRedirect = false, UseCookies = false };
        using var http = new System.Net.Http.HttpClient(handler) { BaseAddress = new Uri(_options!.BaseUrl) };
        var verifier = new string('a', 64);
        static string Base64Url(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
        var challenge = Base64Url(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(verifier)));
        var auth = $"realms/{realmId}/protocol/openid-connect/auth?client_id={ClientId}&redirect_uri={Uri.EscapeDataString(RedirectUri)}&response_type=code&scope=openid&state=integration&nonce=integration&code_challenge_method=S256&code_challenge={challenge}";
        using var authResponse = await http.GetAsync(auth);
        authResponse.EnsureSuccessStatusCode();
        var html = await authResponse.Content.ReadAsStringAsync();
        // Browsers treat localhost as trustworthy for Secure cookies; HttpClient does not.
        // Return this local test server's cookies explicitly when posting its login form.
        var cookies = authResponse.Headers.GetValues("Set-Cookie").Select(c => c.Split(';')[0]);
        http.DefaultRequestHeaders.Add("Cookie", string.Join("; ", cookies));
        var action = System.Text.RegularExpressions.Regex.Match(html, "<form[^>]*action=\"([^\"]+)\"").Groups[1].Value;
        action.ShouldNotBeNullOrWhiteSpace();
        var actionUri = new Uri(System.Net.WebUtility.HtmlDecode(action));
        http.BaseAddress!.IsLoopback.ShouldBeTrue();
        actionUri.GetLeftPart(UriPartial.Authority).ShouldBe(http.BaseAddress.GetLeftPart(UriPartial.Authority));
        using var login = await http.PostAsync(actionUri, new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["username"] = username,
            ["password"] = "test-password",
            ["credentialId"] = "",
        }));
        var loginHtml = await login.Content.ReadAsStringAsync();
        var loginError = System.Text.RegularExpressions.Regex.Match(loginHtml, "<p[^>]*class=\"[^\"]*instruction[^\"]*\"[^>]*>([^<]*)").Groups[1].Value;
        login.StatusCode.ShouldBe(System.Net.HttpStatusCode.Found, loginError);
        var query = login.Headers.Location!.Query.TrimStart('?').Split('&').Select(p => p.Split('=', 2)).ToDictionary(p => p[0], p => Uri.UnescapeDataString(p[1]));
        query.ContainsKey("code").ShouldBeTrue();
        using var response = await http.PostAsync($"realms/{realmId}/protocol/openid-connect/token", new System.Net.Http.FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = ClientId,
            ["client_secret"] = ClientSecret,
            ["redirect_uri"] = RedirectUri,
            ["code"] = query["code"],
            ["code_verifier"] = verifier,
        }));
        response.EnsureSuccessStatusCode();
        return System.Text.Json.JsonDocument.Parse(await response.Content.ReadAsStringAsync());
    }

    private static string[] ReadRoles(string token)
    {
        var payload = token.Split('.')[1].Replace('-', '+').Replace('_', '/');
        using var json = System.Text.Json.JsonDocument.Parse(Convert.FromBase64String(payload.PadRight((payload.Length + 3) / 4 * 4, '=')));
        return json.RootElement.TryGetProperty("roles", out var roles) ? roles.EnumerateArray().Select(r => r.GetString()!).Order().ToArray() : [];
    }

    private static async Task ConfigureKeycloakAsync(string realmId)
    {
        var configurator = _serviceProvider!.GetRequiredService<KeycloakConfigurator>();

        await configurator.ConfigureAsync(
                builder => ConfigureDesiredState(builder, realmId),
                CancellationToken.None)
            .ConfigureAwait(false);
    }

    private static void ConfigureDesiredState(KeycloakConfigurationBuilder builder, string realmId)
    {
        builder.AddRealm(realmId, realm =>
        {
            realm.WithDisplayName("Sample App");
            realm.Enabled();

            realm.AddRole("admin");
            realm.AddRole("scheduler");

            realm.AddClient(ClientId, client =>
            {
                client.WithName("Sample App Web");
                client.Confidential();
                client.WithStandardFlow();
                client.WithSecret(ClientSecret);
                client.WithRedirectUri(RedirectUri);
                client.WithPostLogoutRedirectUri(PostLogoutRedirectUri);
                client.AddRole("admin");
                client.AddRole("scheduler");
                client.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"));
            });

            if (!_options!.SeedTestUser)
            {
                return;
            }

            realm.AddUser(SeedUsername, user =>
            {
                user.WithFirstName("Test");
                user.WithLastName("User");
                user.WithEmail("test-user@vandertil.org");
                user.InClientRole(ClientId, "admin");
                user.WithTemporaryPassword(TemporaryPassword);
            });
        });
    }

    private static string CreateUniqueRealmId()
    {
        return $"sa-{Guid.NewGuid():N}"[..15];
    }

    private static async Task<TClient> CreateClientAsync<TClient>() where TClient : class
    {
        var factory = _serviceProvider!.GetRequiredService<RefitClientFactory>();
        var accessTokenProvider = _serviceProvider!.GetRequiredService<OpenIdConnectAccessTokenProvider>();
        var accessToken = await accessTokenProvider.GetAccessTokenAsync(CancellationToken.None);

        return factory.Create<TClient>(accessToken);
    }

    private static void EnsureIntegrationReady()
    {
        _container.ShouldNotBeNull();
        _serviceProvider.ShouldNotBeNull();
        _options.ShouldNotBeNull();
    }
}