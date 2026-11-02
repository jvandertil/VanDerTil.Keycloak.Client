using System.Linq;

using VanDerTil.Keycloak.Client.Client;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class ClientScopeReconciliationTests
{
    private const string ClientId = "sample-app-web";

    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_KeepScopeRoles_When_ConfiguredAgain()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetScopeRolesAsync(realmId, "View", "Manage");

        // Act
        await SetScopeRolesAsync(realmId, "View", "Manage");

        // Assert
        (await GetScopeRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_RemoveObsoleteScopeRolesAndPreserveOtherClientGrants()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetScopeRolesAsync(realmId, "View", "Manage");
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            realm.AddClient("other", client => client.AddRole("Unrelated"));
            realm.AddClient(ClientId, client => client.WithClientRoleScope("other", "Unrelated"));
        });

        // Act
        await SetScopeRolesAsync(realmId, "View");

        // Assert
        (await GetScopeRolesAsync(realmId)).ShouldBe(new[] { "View" });
        (await GetScopeRolesAsync(realmId, "other")).ShouldBe(new[] { "Unrelated" });
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        var roles = await _fixture.CreateClientAsync<IClientRolesResource>();
        (await roles.GetAllAsync(realmId, client.Id!, CancellationToken.None)).Select(role => role.Name).Order()
            .ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_PreserveScopeRoles_When_MappingsAreOmitted()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetScopeRolesAsync(realmId, "View", "Manage");

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client => client.WithName("Renamed")));

        // Assert
        (await GetScopeRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_ClearScopeRoles_When_AnEmptyListIsConfigured()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetScopeRolesAsync(realmId, "View", "Manage");

        // Act
        await SetScopeRolesAsync(realmId);

        // Assert
        (await GetScopeRolesAsync(realmId)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Should_RejectUnknownScopeRolesWithoutChangingMappings()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetScopeRolesAsync(realmId, "View", "Manage");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => SetScopeRolesAsync(realmId, "MissingPermission"));

        // Assert
        exception.Message.ShouldContain("MissingPermission");
        (await GetScopeRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    private async Task<string> CreateRoleCatalogueAsync()
    {
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client => client.AddRole("View").AddRole("Manage")));
        return realmId;
    }

    private Task SetScopeRolesAsync(string realmId, params string[] roles)
    {
        return _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client => client.WithClientRoleScope(ClientId, roles)));
    }

    private async Task<string?[]> GetScopeRolesAsync(string realmId, string roleClientId = ClientId)
    {
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        var roleClient = await _fixture.GetClientAsync(realmId, roleClientId);
        var scopes = await _fixture.CreateClientAsync<IClientScopeMappingsResource>();
        return (await scopes.GetAsync(realmId, client.Id!, roleClient.Id!, CancellationToken.None))
            .Select(role => role.Name).Order().ToArray();
    }}
