using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class GroupRoleReconciliationTests
{
    private const string ClientId = "sample-app-web";

    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_KeepGroupRoles_When_ConfiguredAgain()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetGroupRolesAsync(realmId, "View", "Manage");

        // Act
        await SetGroupRolesAsync(realmId, "View", "Manage");

        // Assert
        (await GetGroupRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_RemoveObsoleteGroupRolesAndPreserveOtherClientGrants()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetGroupRolesAsync(realmId, "View", "Manage");
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            realm.AddClient("other", client => client.AddRole("Unrelated"));
            realm.AddGroup("Developer", group => group.WithClientRoles("other", "Unrelated"));
        });

        // Act
        await SetGroupRolesAsync(realmId, "View");

        // Assert
        (await GetGroupRolesAsync(realmId)).ShouldBe(new[] { "View" });
        (await GetGroupRolesAsync(realmId, "other")).ShouldBe(new[] { "Unrelated" });
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        var roles = await _fixture.CreateClientAsync<IClientRolesResource>();
        (await roles.GetAllAsync(realmId, client.Id!, CancellationToken.None)).Select(role => role.Name).Order()
            .ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_PreserveGroupRoles_When_MappingsAreOmitted()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetGroupRolesAsync(realmId, "View", "Manage");

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddGroup("Developer", group => { }));

        // Assert
        (await GetGroupRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_ClearGroupRoles_When_AnEmptyListIsConfigured()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetGroupRolesAsync(realmId, "View", "Manage");

        // Act
        await SetGroupRolesAsync(realmId);

        // Assert
        (await GetGroupRolesAsync(realmId)).ShouldBeEmpty();
    }

    [TestMethod]
    public async Task Should_RejectUnknownGroupRolesWithoutChangingMappings()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await SetGroupRolesAsync(realmId, "View", "Manage");

        // Act
        var exception = await Should.ThrowAsync<InvalidOperationException>(() => SetGroupRolesAsync(realmId, "MissingPermission"));

        // Assert
        exception.Message.ShouldContain("MissingPermission");
        (await GetGroupRolesAsync(realmId)).ShouldBe(new[] { "Manage", "View" });
    }

    [TestMethod]
    public async Task Should_CreateTopLevelGroupWithoutChangingSameNamedNestedGroup()
    {
        // Arrange
        var realmId = await CreateRoleCatalogueAsync();
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddGroup("Parent", group => { }));
        var groups = await _fixture.CreateClientAsync<IGroupsResource>();
        using var parent = await groups.GetByPathAsync(realmId, "Parent", CancellationToken.None);
        using var nested = await groups.CreateChildAsync(realmId, parent.Content!.Id!, new GroupRepresentation { Name = "Developer" }, CancellationToken.None);
        nested.IsSuccessStatusCode.ShouldBeTrue();
        var nestedGroupId = nested.Headers.Location!.Segments[^1].TrimEnd('/');

        // Act
        await SetGroupRolesAsync(realmId, "View");

        // Assert
        (await GetGroupRolesAsync(realmId)).ShouldBe(new[] { "View" });
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        (await groups.GetRolesAsync(realmId, nestedGroupId, client.Id!, CancellationToken.None)).ShouldBeEmpty();
    }

    private async Task<string> CreateRoleCatalogueAsync()
    {
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client => client.AddRole("View").AddRole("Manage")));
        return realmId;
    }

    private Task SetGroupRolesAsync(string realmId, params string[] roles)
    {
        return _fixture.ConfigureRealmAsync(realmId, realm => realm.AddGroup("Developer", group => group.WithClientRoles(ClientId, roles)));
    }

    private async Task<string?[]> GetGroupRolesAsync(string realmId, string roleClientId = ClientId)
    {
        var groups = await _fixture.CreateClientAsync<IGroupsResource>();
        using var group = await groups.GetByPathAsync(realmId, "Developer", CancellationToken.None);
        var client = await _fixture.GetClientAsync(realmId, roleClientId);
        return (await groups.GetRolesAsync(realmId, group.Content!.Id!, client.Id!, CancellationToken.None))
            .Select(role => role.Name).Order().ToArray();
    }
}