using Microsoft.Extensions.Configuration;

using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class UserConfigurationTests
{
    [TestMethod]
    public void Should_ApplyTemporaryPasswordFromConfiguration()
    {
        // Arrange
        var configuration = CreateUserConfiguration("configured-password");
        var builder = new UserBuilder("test-user").WithTemporaryPassword("code-password");

        // Act
        builder.WithConfiguration(configuration);

        // Assert
        var credential = builder.Build().Credentials.ShouldHaveSingleItem();
        credential.Type.ShouldBe("password");
        credential.Value.ShouldBe("configured-password");
        credential.Temporary.ShouldBeTrue();
    }

    [TestMethod]
    [DataRow(null)]
    [DataRow("   ")]
    public void Should_PreservePassword_When_ConfigurationHasNoPassword(string? configuredPassword)
    {
        // Arrange
        var configuration = CreateUserConfiguration(configuredPassword);
        var builder = new UserBuilder("test-user").WithTemporaryPassword("code-password");

        // Act
        builder.WithConfiguration(configuration);

        // Assert
        builder.Build().Credentials.ShouldHaveSingleItem().Value.ShouldBe("code-password");
    }

    [TestMethod]
    public void Should_RejectNullConfigurationSection()
    {
        // Arrange
        var builder = new UserBuilder("test-user");

        // Act
        Action configure = () => builder.WithConfiguration(null!);

        // Assert
        Should.Throw<ArgumentNullException>(configure);
    }

    [TestMethod]
    public void Should_OmitCredentials_When_NoPasswordIsConfigured()
    {
        // Arrange
        var builder = new UserBuilder("test-user");

        // Act
        var user = builder.Build();

        // Assert
        user.Credentials.ShouldBeNull();
    }

    private static IConfigurationSection CreateUserConfiguration(string? temporaryPassword)
    {
        var settings = new Dictionary<string, string?>();
        if (temporaryPassword is not null)
        {
            settings["User:TemporaryPassword"] = temporaryPassword;
        }

        return new ConfigurationBuilder()
            .AddInMemoryCollection(settings)
            .Build()
            .GetSection("User");
    }
}
