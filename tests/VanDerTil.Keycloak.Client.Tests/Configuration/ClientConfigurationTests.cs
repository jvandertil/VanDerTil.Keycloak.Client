using System.Linq;

using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class ClientConfigurationTests
{
    [TestClass]
    public sealed class ConfiguringFromConfigurationSection
    {
        [TestMethod]
        public void Should_ApplySecretRedirectUrisAndPostLogoutRedirectUris()
        {
            // Arrange
            var builder = new KeycloakConfigurationBuilder();

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddClient("sample-app-web", client =>
                {
                    client.WithSecret("configured-secret");
                    client.WithRedirectUri("https://configured.example/signin-oidc");
                    client.WithRedirectUri("https://code.example/signin-oidc");
                });
            });

            // Assert
            var realm = builder.Realms.Single();
            var client = realm.Clients.Single().Build();

            client.Secret.ShouldBe("configured-secret");

            client.RedirectUris.ShouldNotBeNull();
            client.RedirectUris.Count.ShouldBe(2);
            client.RedirectUris.ShouldContain("https://code.example/signin-oidc");
            client.RedirectUris.ShouldContain("https://configured.example/signin-oidc");

            client.Attributes.ShouldBeNull();
        }
    }

    [TestClass]
    public sealed class AddingClientRoles
    {
        [TestMethod]
        public void Should_TrackRoles()
        {
            // Arrange
            var builder = new KeycloakConfigurationBuilder();
            ClientBuilder? clientBuilder = null;

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddClient("sample-app-web", client =>
                {
                    clientBuilder = client;
                    client.AddRole("admin");
                    client.AddRole("scheduler");
                });
            });

            // Assert
            clientBuilder.ShouldNotBeNull();
            clientBuilder.Roles.ShouldContain("admin");
            clientBuilder.Roles.ShouldContain("scheduler");
        }
    }

    [TestClass]
    public sealed class AddingProtocolMappers
    {
        [TestMethod]
        public void Should_TrackMapper()
        {
            // Arrange
            var builder = new KeycloakConfigurationBuilder();
            ClientBuilder? clientBuilder = null;

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddClient("sample-app-web", client =>
                {
                    clientBuilder = client;
                    client.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"));
                });
            });

            // Assert
            clientBuilder.ShouldNotBeNull();
            clientBuilder.ProtocolMappers.ShouldHaveSingleItem();

            var mapper = clientBuilder.ProtocolMappers.Single().Build();
            mapper.Name.ShouldBe("client-roles");
            mapper.ProtocolMapper.ShouldBe("oidc-usermodel-client-role-mapper");
            mapper.Config.ShouldNotBeNull();
            mapper.Config["claim.name"].ShouldBe("roles");
            mapper.Config["multivalued"].ShouldBe("true");
        }

        [TestMethod]
        public void Should_ThrowArgumentNullException_When_MapperBuilderIsNull()
        {
            // Arrange
            var builder = new KeycloakConfigurationBuilder();
            ClientBuilder? clientBuilder = null;

            builder.AddRealm("sample-app", realm =>
            {
                realm.AddClient("sample-app-web", client =>
                {
                    clientBuilder = client;
                });
            });

            // Act & Assert
            Should.Throw<ArgumentNullException>(() => clientBuilder!.AddProtocolMapper(null!));
        }
    }
}
