using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class ClientConfigurationTests
{
    [TestMethod]
    public void Should_CopyRealmRoleScopeNames()
    {
        // Arrange
        var builder = new ClientBuilder("web");
        string[] names = ["reader"];

        // Act
        builder.WithRealmRoleScope(names);
        names[0] = "changed";

        // Assert
        builder.RealmRoleScopes.ShouldBe(new[] { "reader" });
    }

    [TestMethod]
    public void Should_RejectNullRealmRoleScopes()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.WithRealmRoleScope(null!);

        // Assert
        Should.Throw<ArgumentNullException>(configure);
    }

    [TestMethod]
    public void Should_RejectWhitespaceRealmRoleScopeNames()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.WithRealmRoleScope(" ");

        // Assert
        Should.Throw<ArgumentException>(configure);
    }

    [TestMethod]
    public void Should_ClearRealmRoleScopes_When_NoNamesAreProvided()
    {
        // Arrange
        var builder = new ClientBuilder("web").WithRealmRoleScope("reader");

        // Act
        builder.WithRealmRoleScope();

        // Assert
        builder.RealmRoleScopes.ShouldBeEmpty();
    }

    [TestMethod]
    public void Should_IncludeConfiguredSecret()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        var client = builder.WithSecret("configured-secret").Build();

        // Assert
        client.Secret.ShouldBe("configured-secret");
    }

    [TestMethod]
    public void Should_IncludeAllConfiguredRedirectUris()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        var client = builder
            .WithRedirectUri("https://configured.example/signin-oidc")
            .WithRedirectUri("https://code.example/signin-oidc")
            .Build();

        // Assert
        client.RedirectUris.ShouldBe(new[]
        {
            "https://configured.example/signin-oidc",
            "https://code.example/signin-oidc",
        }, ignoreOrder: true);
    }

    [TestMethod]
    public void Should_TrackClientRoles()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        builder.AddRole("admin").AddRole("scheduler");

        // Assert
        builder.Roles.ShouldBe(new[] { "admin", "scheduler" }, ignoreOrder: true);
    }

    [TestMethod]
    public void Should_IncludeClientRolesInConfiguredClaim()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        builder.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"));

        // Assert
        var mapper = builder.ProtocolMappers.ShouldHaveSingleItem().Build();
        mapper.Name.ShouldBe("client-roles");
        mapper.ProtocolMapper.ShouldBe("oidc-usermodel-client-role-mapper");
        mapper.Config.ShouldNotBeNull();
        mapper.Config["claim.name"].ShouldBe("roles");
        mapper.Config["multivalued"].ShouldBe("true");
        mapper.Config["jsonType.label"].ShouldBe("String");
        mapper.Config["access.token.claim"].ShouldBe("true");
        mapper.Config["id.token.claim"].ShouldBe("true");
        mapper.Config["userinfo.token.claim"].ShouldBe("true");
    }

    [TestMethod]
    public void Should_AccumulateBulkValuesWithSingularConfiguration()
    {
        // Arrange
        var builder = new ClientBuilder("web")
            .WithRedirectUri("https://first.example/signin")
            .WithPostLogoutRedirectUri("https://first.example/signout")
            .AddRole("reader");
        string[] roles = ["writer", "scheduler"];

        // Act
        builder.WithRedirectUris("https://second.example/signin", "https://third.example/signin")
            .WithPostLogoutRedirectUris("https://second.example/signout", "https://third.example/signout")
            .AddRoles(roles)
            .WithRedirectUris()
            .WithPostLogoutRedirectUris()
            .AddRoles();
        roles[0] = "changed";
        var client = builder.Build();

        // Assert
        client.RedirectUris.ShouldBe(new[]
        {
            "https://first.example/signin", "https://second.example/signin", "https://third.example/signin"
        });
        client.Attributes.ShouldNotBeNull()["post.logout.redirect.uris"].ShouldBe(
            "https://first.example/signout##https://second.example/signout##https://third.example/signout");
        builder.Roles.ShouldBe(new[] { "reader", "writer", "scheduler" });
    }

    [TestMethod]
    public void Should_RejectNullBulkCollections()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act / Assert
        Should.Throw<ArgumentNullException>(() => builder.WithRedirectUris(null!));
        Should.Throw<ArgumentNullException>(() => builder.WithPostLogoutRedirectUris(null!));
        Should.Throw<ArgumentNullException>(() => builder.AddRoles(null!));
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("")]
    [DataRow(" ")]
    public void Should_ValidateEachBulkValue(string? value)
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act / Assert
        Should.Throw<ArgumentException>(() => builder.WithRedirectUris([value!]));
        Should.Throw<ArgumentException>(() => builder.WithPostLogoutRedirectUris([value!]));
        Should.Throw<ArgumentException>(() => builder.AddRoles([value!]));
    }

    [TestMethod]
    public void Should_AllowOverridingMapperFactoryDefaults()
    {
        // Arrange
        var builder = ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles");

        // Act
        var mapper = builder.WithConfig("id.token.claim", "false").Build();

        // Assert
        mapper.Config.ShouldNotBeNull()["id.token.claim"].ShouldBe("false");
        mapper.Config["userinfo.token.claim"].ShouldBe("true");
    }

    [TestMethod]
    public void Should_RejectNullProtocolMapper()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.AddProtocolMapper(null!);

        // Assert
        Should.Throw<ArgumentNullException>(configure);
    }
}
