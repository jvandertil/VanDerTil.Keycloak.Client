using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client;

public static class KeycloakServiceCollectionExtensions
{
    public static IServiceCollection AddKeycloakClient(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<KeycloakConnectionOptions>? configureOptions = null)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<KeycloakConnectionOptions>()
            .Bind(configuration.GetSection(KeycloakConnectionOptions.SectionName))
            .Validate(options => Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _), "The Keycloak connection base URL is invalid.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AdminUsername), "The Keycloak admin username is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AdminPassword), "The Keycloak admin password is required.")
            .Validate(options => !string.IsNullOrWhiteSpace(options.AdminRealm), "The Keycloak admin realm is required.");

        if (configureOptions is not null)
        {
            services.Configure(configureOptions);
        }

        services.AddHttpClient(OpenIdConnectAccessTokenProvider.HttpClientName, static client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddHttpClient(RefitClientFactory.HttpClientName, static (serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<KeycloakConnectionOptions>>().Value;
            client.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        services.AddSingleton<OpenIdConnectAccessTokenProvider>();
        services.AddSingleton<RefitClientFactory>();
        services.AddSingleton<KeycloakConfigurator>();
        services.AddSingleton<IKeycloakConfigurator>(serviceProvider => serviceProvider.GetRequiredService<KeycloakConfigurator>());

        return services;
    }
}
