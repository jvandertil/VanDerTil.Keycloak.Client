using System.Linq;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

using Testcontainers.Keycloak;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

internal sealed class KeycloakFixture : IAsyncDisposable
{
    private static Lazy<Task<KeycloakFixture>>? _shared;
    private readonly KeycloakContainer _container;
    private readonly ServiceProvider _services;

    private KeycloakFixture(KeycloakContainer container, ServiceProvider services)
    {
        _container = container;
        _services = services;
        BaseAddress = new Uri(container.GetBaseAddress().ToString());
    }

    internal Uri BaseAddress { get; }

    internal static void Initialize()
    {
        // Register the fixture without contacting Docker during unit-only runs.
        _shared = new Lazy<Task<KeycloakFixture>>(CreateAsync);
    }

    internal static Task<KeycloakFixture> GetAsync()
    {
        return (_shared ?? throw new InvalidOperationException("The assembly fixture has not been initialized.")).Value;
    }

    internal static async Task DisposeSharedAsync()
    {
        if (_shared is { IsValueCreated: true } && _shared.Value.IsCompletedSuccessfully)
        {
            var fixture = await _shared.Value;
            await fixture.DisposeAsync();
        }
    }

    internal static string CreateUniqueRealmId() => $"sa-{Guid.NewGuid():N}"[..15];

    internal Task ConfigureRealmAsync(string realmId, Action<RealmBuilder> configure)
    {
        return _services.GetRequiredService<KeycloakConfigurator>()
            .ConfigureAsync(builder => builder.AddRealm(realmId, configure), CancellationToken.None);
    }

    internal async Task<TClient> CreateClientAsync<TClient>() where TClient : class
    {
        var token = await _services.GetRequiredService<OpenIdConnectAccessTokenProvider>()
            .GetAccessTokenAsync(CancellationToken.None);
        return _services.GetRequiredService<RefitClientFactory>().Create<TClient>(token);
    }

    internal async Task<ClientRepresentation> GetClientAsync(string realmId, string clientId)
    {
        var clients = await CreateClientAsync<IClientsResource>();
        return (await clients.GetAllAsync(realmId)).Single(client => client.ClientId == clientId);
    }

    internal async Task<UserRepresentation> GetUserAsync(string realmId, string username)
    {
        var users = await CreateClientAsync<IUsersResource>();
        return (await users.GetAllAsync(realmId)).Single(user => user.Username == username);
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await _services.DisposeAsync();
        }
        finally
        {
            await _container.DisposeAsync();
        }
    }

    private static async Task<KeycloakFixture> CreateAsync()
    {
        const string adminUsername = "admin";
        const string adminPassword = "admin";
        var container = new KeycloakBuilder("quay.io/keycloak/keycloak:26.8")
            .WithUsername(adminUsername)
            .WithPassword(adminPassword)
            .Build();
        ServiceProvider? serviceProvider = null;
        try
        {
            await container.StartAsync();
            var services = new ServiceCollection();
            services.AddLogging(logging => logging.AddConsole().SetMinimumLevel(LogLevel.Warning));
            services.AddOptions<KeycloakConnectionOptions>().Configure(options =>
            {
                options.BaseUrl = container.GetBaseAddress().ToString();
                options.AuthenticationMode = AuthenticationMode.DirectGrant;
                options.AdminUsername = adminUsername;
                options.AdminPassword = adminPassword;
                options.AdminRealm = "master";
                options.SeedTestUser = true;
            });
            services.AddHttpClient(OpenIdConnectAccessTokenProvider.HttpClientName,
                client => client.Timeout = TimeSpan.FromSeconds(30));
            services.AddHttpClient(RefitClientFactory.HttpClientName, (provider, client) =>
            {
                client.BaseAddress = new Uri(provider.GetRequiredService<IOptions<KeycloakConnectionOptions>>().Value.BaseUrl);
                client.Timeout = TimeSpan.FromSeconds(30);
            });
            services.AddSingleton<OpenIdConnectAccessTokenProvider>();
            services.AddSingleton<RefitClientFactory>();
            services.AddSingleton<KeycloakConfigurator>();
            serviceProvider = services.BuildServiceProvider();
            return new KeycloakFixture(container, serviceProvider);
        }
        catch
        {
            if (serviceProvider is not null)
            {
                await serviceProvider.DisposeAsync();
            }
            await container.DisposeAsync();
            throw;
        }
    }
}