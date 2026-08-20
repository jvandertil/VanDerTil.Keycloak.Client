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
