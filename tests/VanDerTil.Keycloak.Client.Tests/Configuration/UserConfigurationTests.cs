using System.Linq;

using Microsoft.Extensions.Configuration;

using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

public sealed class UserConfigurationTests
{
    [TestClass]
    public sealed class ConfiguringFromConfigurationSection
    {
        [TestMethod]
        public void Should_ApplyTemporaryPassword_When_PasswordIsProvidedInSection()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["User:TemporaryPassword"] = "configured-password",
                })
                .Build();

            var builder = new KeycloakConfigurationBuilder();

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddUser("test-user", user =>
                {
                    user.WithTemporaryPassword("code-password");
                    user.WithConfiguration(configuration.GetSection("User"));
                });
            });

            // Assert
            var realm = builder.Realms.Single();
            var user = realm.Users.Single().Build();

            var credential = user.Credentials.ShouldHaveSingleItem();
            credential.Type.ShouldBe("password");
            credential.Value.ShouldBe("configured-password");
            credential.Temporary.ShouldBeTrue();
        }

        [TestMethod]
        public void Should_NotOverridePassword_When_PasswordKeyIsAbsent()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>())
                .Build();

            var builder = new KeycloakConfigurationBuilder();

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddUser("test-user", user =>
                {
                    user.WithTemporaryPassword("code-password");
                    user.WithConfiguration(configuration.GetSection("User"));
                });
            });

            // Assert
            var user = builder.Realms.Single().Users.Single().Build();

            user.Credentials.ShouldHaveSingleItem().Value.ShouldBe("code-password");
        }

        [TestMethod]
        public void Should_NotOverridePassword_When_PasswordValueIsWhitespace()
        {
            // Arrange
            var configuration = new ConfigurationBuilder()
                .AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["User:TemporaryPassword"] = "   ",
                })
                .Build();

            var builder = new KeycloakConfigurationBuilder();

            // Act
            builder.AddRealm("sample-app", realm =>
            {
                realm.AddUser("test-user", user =>
                {
                    user.WithTemporaryPassword("code-password");
                    user.WithConfiguration(configuration.GetSection("User"));
                });
            });

            // Assert
            var user = builder.Realms.Single().Users.Single().Build();

            user.Credentials.ShouldHaveSingleItem().Value.ShouldBe("code-password");
        }

        [TestMethod]
        public void Should_ThrowArgumentNullException_When_ConfigurationSectionIsNull()
        {
            // Arrange
            var builder = new KeycloakConfigurationBuilder();
            UserBuilder? userBuilder = null;

            builder.AddRealm("sample-app", realm =>
            {
                realm.AddUser("test-user", user =>
                {
                    userBuilder = user;
                });
            });

            // Act & Assert
            Should.Throw<ArgumentNullException>(() => userBuilder!.WithConfiguration(null!));
        }
    }

    [TestClass]
    public sealed class WithNoPasswordConfigured
    {
        [TestMethod]
        public void Should_NotSetCredentials()
        {
            var builder = new KeycloakConfigurationBuilder();

            builder.AddRealm("sample-app", realm =>
            {
                realm.AddUser("test-user", user => { });
            });

            var user = builder.Realms.Single().Users.Single().Build();

            user.Credentials.ShouldBeNull();
        }
    }
}
