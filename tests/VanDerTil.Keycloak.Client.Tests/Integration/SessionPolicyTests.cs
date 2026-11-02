using System.Text.Json;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class SessionPolicyTests
{
    private const string ClientId = "sample-app-web";
    private const string ClientSecret = "integration-client-secret";
    private const string RedirectUri = "https://localhost:7257/signin-oidc";

    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_PreserveUnspecifiedClientSettings_When_UpdatingName()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            ConfigureRealmPolicy(realm);
            realm.AddClient(ClientId, ConfigureClientPolicy);
        });
        var clients = await _fixture.CreateClientAsync<IClientsResource>();
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        client.AdditionalProperties!["directAccessGrantsEnabled"] = JsonSerializer.SerializeToElement(true);
        await clients.UpdateAsync(realmId, client.Id!, client);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client => client.WithName("Renamed")));

        // Assert
        client = await _fixture.GetClientAsync(realmId, ClientId);
        client.Name.ShouldBe("Renamed");
        client.FullScopeAllowed.ShouldBe(false);
        client.Attributes.ShouldNotBeNull()["pkce.code.challenge.method"].ShouldBe("S256");
        client.Attributes["access.token.lifespan"].ShouldBe("300");
        client.Attributes["client.session.idle.timeout"].ShouldBe("2100");
        client.Attributes["client.session.max.lifespan"].ShouldBe("28800");
        client.Attributes["use.refresh.tokens"].ShouldBe("true");
        client.AdditionalProperties!["directAccessGrantsEnabled"].GetBoolean().ShouldBeTrue();
    }

    [TestMethod]
    public async Task Should_UpdateConfiguredClientPolicySettings()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            ConfigureRealmPolicy(realm);
            realm.AddClient(ClientId, ConfigureClientPolicy);
        });

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client =>
        {
            client.WithFullScopeAllowed(true);
            client.WithRefreshTokens(false);
            client.WithAccessTokenLifespan(TimeSpan.FromMinutes(2));
        }));

        // Assert
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        client.FullScopeAllowed.ShouldBe(true);
        client.Attributes.ShouldNotBeNull()["use.refresh.tokens"].ShouldBe("false");
        client.Attributes["access.token.lifespan"].ShouldBe("120");
    }

    [TestMethod]
    public async Task Should_PreserveUnspecifiedRealmSessionPolicy_When_UpdatingDisplayName()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRealmPolicy);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.WithDisplayName("Renamed"));

        // Assert
        var realms = await _fixture.CreateClientAsync<IRealmResource>();
        var realm = await realms.GetAsync(realmId);
        realm.DisplayName.ShouldBe("Renamed");
        realm.RevokeRefreshToken.ShouldBe(true);
        realm.RefreshTokenMaxReuse.ShouldBe(0);
        realm.SsoSessionIdleTimeout.ShouldBe(2400);
        realm.SsoSessionMaxLifespan.ShouldBe(32400);
    }

    [TestMethod]
    public async Task Should_UpdateConfiguredRealmSessionPolicy()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRealmPolicy);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            realm.WithRefreshTokenRotation(false, maxReuse: 2);
            realm.WithSsoSessionIdleTimeout(TimeSpan.FromMinutes(50));
        });

        // Assert
        var realms = await _fixture.CreateClientAsync<IRealmResource>();
        var realm = await realms.GetAsync(realmId);
        realm.RevokeRefreshToken.ShouldBe(false);
        realm.RefreshTokenMaxReuse.ShouldBe(2);
        realm.SsoSessionIdleTimeout.ShouldBe(3000);
        realm.SsoSessionMaxLifespan.ShouldBe(32400);
    }

    private static void ConfigureClientPolicy(ClientBuilder client)
    {
        client.Confidential().WithSecret(ClientSecret).WithStandardFlow().WithRedirectUri(RedirectUri);
        client.WithFullScopeAllowed(false).WithPkceS256().WithRefreshTokens(true);
        client.WithAccessTokenLifespan(TimeSpan.FromMinutes(5));
        client.WithClientSessionIdleTimeout(TimeSpan.FromMinutes(35));
        client.WithClientSessionMaxLifespan(TimeSpan.FromHours(8));
    }

    private static void ConfigureRealmPolicy(RealmBuilder realm)
    {
        realm.WithRefreshTokenRotation(true);
        realm.WithSsoSessionIdleTimeout(TimeSpan.FromMinutes(40));
        realm.WithSsoSessionMaxLifespan(TimeSpan.FromHours(9));
    }
}