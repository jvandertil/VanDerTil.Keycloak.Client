using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class ClientProvisioningTests
{
    private const string ClientId = "sample-app-web";
    private const string ClientSecret = "integration-client-secret";
    private const string RedirectUri = "https://localhost:7257/signin-oidc";
    private const string PostLogoutRedirectUri = "https://localhost:7257/signout-callback-oidc";

    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_ConfigureConfidentialClientRedirects()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client =>
        {
            client.Confidential().WithSecret(ClientSecret).WithStandardFlow();
            client.WithRedirectUri(RedirectUri).WithPostLogoutRedirectUri(PostLogoutRedirectUri);
        }));

        // Assert
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        client.PublicClient.ShouldBeFalse();
        client.FullScopeAllowed.ShouldBeFalse();
        client.StandardFlowEnabled.ShouldBeTrue();
        client.RedirectUris.ShouldNotBeNull().ShouldBe(new[] { RedirectUri });
        client.Attributes.ShouldNotBeNull()["post.logout.redirect.uris"].Split("##").ShouldBe(new[] { PostLogoutRedirectUri });
    }

    [TestMethod]
    public async Task Should_CreateClientRolesWithoutDuplicates_When_ConfiguredAgain()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();
        static void ConfigureRoles(RealmBuilder realm) => realm.AddClient(ClientId, client => client.AddRole("admin").AddRole("scheduler"));
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRoles);

        // Act
        await _fixture.ConfigureRealmAsync(realmId, ConfigureRoles);

        // Assert
        var clients = await _fixture.CreateClientAsync<IClientsResource>();
        var client = (await clients.GetAllAsync(realmId)).Where(client => client.ClientId == ClientId).ShouldHaveSingleItem();
        var roles = await _fixture.CreateClientAsync<IClientRolesResource>();
        (await roles.GetAllAsync(realmId, client.Id!, CancellationToken.None)).Select(role => role.Name).Order()
            .ShouldBe(new[] { "admin", "scheduler" });
    }

    [TestMethod]
    public async Task Should_CreateClientRoleMapper()
    {
        // Arrange
        var realmId = KeycloakFixture.CreateUniqueRealmId();

        // Act
        await _fixture.ConfigureRealmAsync(realmId, realm => realm.AddClient(ClientId, client =>
            client.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"))));

        // Assert
        var client = await _fixture.GetClientAsync(realmId, ClientId);
        var mappers = await _fixture.CreateClientAsync<IClientProtocolMappersResource>();
        var mapper = (await mappers.GetAllAsync(realmId, client.Id!, CancellationToken.None))
            .Where(mapper => mapper.Name == "client-roles").ShouldHaveSingleItem();
        mapper.ProtocolMapper.ShouldBe("oidc-usermodel-client-role-mapper");
        mapper.Config.ShouldNotBeNull()["claim.name"].ShouldBe("roles");
    }

}