using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class UserProvisioningTests
{
    private const string ClientId = "sample-app-web";
    private const string SeedUsername = "test-user";

    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_AssignClientRoleWithoutDuplicatingUser_When_ConfiguredAgain()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        static void ConfigureUser(RealmBuilder realm)
        {
            realm.AddClient(ClientId, client => client.AddRole("admin"));
            realm.AddUser(SeedUsername, user => user.InClientRole(ClientId, "admin"));
        }
        await _fixture.ConfigureRealmAsync(realmId, ConfigureUser);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, ConfigureUser);

        // Assert
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        var users = await _fixture.CreateClientAsync<IUsersResource>();
        var user = (await users.GetAllAsync(realmId)).Where(user => user.Username == SeedUsername).ShouldHaveSingleItem();
        (await users.GetClientRoleMappingsAsync(realmId, user.Id!, client.Id!, CancellationToken.None))
            .Select(role => role.Name).ShouldBe(new[] { "admin" });
    }
    [TestMethod]
    public async Task Should_PreserveUnmanagedUsersAndMemberships_When_ConfiguringUserAgain()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        await _fixture.ConfigureRealmAsync(realmId, realm =>
        {
            realm.AddGroup("Developer", group => { });
            realm.AddGroup("Other", group => { });
            realm.AddUser("unrelated-user", user => user.WithFirstName("Preserved"));
            realm.AddUser("member", user => user.InGroup("Developer").InGroup("Other"));
        });

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddUser("member", user => user.InGroup("Developer")));

        // Assert
        (await _fixture.GetUserAsync(realmId, "unrelated-user")).FirstName.ShouldBe("Preserved");
        var member = await _fixture.GetUserAsync(realmId, "member");
        var users = await _fixture.CreateClientAsync<IUsersResource>();
        (await users.GetGroupsAsync(realmId, member.Id!, CancellationToken.None)).Select(group => group.Name).Order()
            .ShouldBe(new[] { "Developer", "Other" });
    }

}
