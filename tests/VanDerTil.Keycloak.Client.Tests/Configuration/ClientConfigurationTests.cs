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