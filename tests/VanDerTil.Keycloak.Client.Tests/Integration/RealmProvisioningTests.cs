using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class RealmProvisioningTests
{
    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_CreateEnabledRealm()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.Enabled());

        // Assert
        var realms = await _fixture.CreateClientAsync<IRealmResource>();
        (await realms.GetAsync(realmId)).Enabled.ShouldBeTrue();
    }

    [TestMethod]
    public async Task Should_CreateRealmRolesWithoutDuplicates_When_ConfiguredAgain()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        static void ConfigureRoles(RealmBuilder realm) => realm.AddRole("admin").AddRole("scheduler");
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRoles);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRoles);

        // Assert
        var roles = await _fixture.CreateClientAsync<IRolesResource>();
        var names = (await roles.GetAllAsync(realmId, CancellationToken.None)).Select(role => role.Name).ToArray();
        names.Count(name => name == "admin").ShouldBe(1);
        names.Count(name => name == "scheduler").ShouldBe(1);
    }

}
