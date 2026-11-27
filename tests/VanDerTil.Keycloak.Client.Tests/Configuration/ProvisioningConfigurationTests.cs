using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class ProvisioningConfigurationTests
{
    [TestMethod]
    public void Build_SnapshotsTheWholeTreeAfterConfiguration()
    {
        var root = new KeycloakConfigurationBuilder();
        RealmBuilder realm = null!;
        ClientBuilder client = null!;
        UserBuilder user = null!;
        GroupBuilder group = null!;
        var mapper = ProtocolMapperBuilder.UserClientRolesToClaim("roles", "original");
        root.AddRealm("one", r =>
        {
            realm = r;
            r.AddClient("web", c => { client = c; c.AddProtocolMapper(mapper).WithClientRoleScope("api", "read").WithRealmRoleScope(); });
            r.AddUser("alice", u => { user = u; u.WithPassword("first").InGroup("staff"); });
            r.AddGroup("staff", g => { group = g; g.WithClientRoles("api", "read"); });
        });
        // Mutations made after child callbacks but before the top-level build are included.
        mapper.WithConfig("claim.name", "before");
        client.AddRole("before");
        var snapshot = root.Build();
        mapper.WithConfig("claim.name", "after");
        client.AddRole("after").WithClientRoleScope("api", "write").WithRealmRoleScope("other");
        user.WithPassword("second").InGroup("other");
        group.WithClientRoles("api", "write");
        realm.AddRole("after");
        root.AddRealm("two", _ => { });

        var savedRealm = snapshot.Realms.ShouldHaveSingleItem();
        var savedClient = savedRealm.Clients.ShouldHaveSingleItem();
        savedClient.Roles.ShouldBe(new[] { "before" });
        savedClient.ProtocolMappers.ShouldHaveSingleItem().Config["claim.name"].ShouldBe("before");
        savedClient.RoleScopes["api"].ShouldBe(new[] { "read" });
        savedClient.RealmRoleScopes.ShouldNotBeNull().ShouldBeEmpty();
        savedRealm.Groups.ShouldHaveSingleItem().ClientRoles["api"].ShouldBe(new[] { "read" });
        var savedUser = savedRealm.Users.ShouldHaveSingleItem();
        savedUser.Password.ShouldBe("first");
        savedUser.Groups.ShouldBe(new[] { "staff" });
        savedRealm.Roles.ShouldBeEmpty();
    }

    [TestMethod]
    public void Build_PreservesMappingOrderAndOrdinalLastWriteWins()
    {
        var client = new ClientBuilder("web")
            .WithClientRoleScope("api", "first")
            .WithClientRoleScope("API", "upper")
            .WithClientRoleScope("api", "last")
            .Build();
        client.RoleScopes.Keys.ShouldBe(new[] { "api", "API" });
        client.RoleScopes["api"].ShouldBe(new[] { "last" });
        client.RoleScopes["API"].ShouldBe(new[] { "upper" });
        new ClientBuilder("other").Build().RealmRoleScopes.ShouldBeNull();
    }

    [TestMethod]
    public void Projections_PreserveCreateAndUpdateDefaultsAndCredentials()
    {
        var client = new ClientBuilder("web").AddPostLogoutRedirectUris("one", "two").Build();
        var created = ConfigurationProjections.CreateClient(client);
        created.FullScopeAllowed.ShouldBe(false);
        created.Attributes!["post.logout.redirect.uris"].ShouldBe("one##two");
        var updated = ConfigurationProjections.UpdateClient(new() { FullScopeAllowed = true, Enabled = false }, created, client);
        updated.FullScopeAllowed.ShouldBe(true);
        updated.Enabled.ShouldBe(false);
        var user = ConfigurationProjections.CreateUser(new UserBuilder("alice").WithTemporaryPassword("temp").WithPassword("permanent").Build());
        user.Enabled.ShouldBe(true);
        user.Credentials!.Select(c => (c.Value, c.Temporary)).ShouldBe(new[] { ("temp", true), ("permanent", false) });
    }
}
