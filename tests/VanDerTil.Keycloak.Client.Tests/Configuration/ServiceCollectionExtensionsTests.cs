using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using VanDerTil.Keycloak.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class ServiceCollectionExtensionsTests
{
    [TestMethod]
    public void Should_RegisterTheConfiguratorAndConnectionOptions()
    {
        var services = new ServiceCollection();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Keycloak:Connection:BaseUrl"] = "https://example.com",
                ["Keycloak:Connection:AdminUsername"] = "admin",
                ["Keycloak:Connection:AdminPassword"] = "password",
                ["Keycloak:Connection:AdminRealm"] = "master",
            })
            .Build();

        services.AddKeycloakClient(configuration);

        using var serviceProvider = services.BuildServiceProvider();
        var options = serviceProvider.GetRequiredService<IOptions<KeycloakConnectionOptions>>().Value;
        var configurator = serviceProvider.GetRequiredService<IKeycloakConfigurator>();

        options.BaseUrl.ShouldBe("https://example.com");
        options.AdminRealm.ShouldBe("master");
        configurator.ShouldNotBeNull();
        configurator.ShouldBeOfType<KeycloakConfigurator>();
    }
}
