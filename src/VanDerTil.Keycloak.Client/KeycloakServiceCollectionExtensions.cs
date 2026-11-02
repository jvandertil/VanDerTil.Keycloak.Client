using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client;

/// <summary>
/// Provides dependency injection registration helpers for the Keycloak client library.
/// </summary>
public static class KeycloakServiceCollectionExtensions
{
    /// <summary>
    /// Registers the services required to connect to Keycloak, acquire admin access tokens, and run configuration provisioning.
    /// </summary>
    /// <param name="services">The service collection to add the Keycloak client services to.</param>
    /// <param name="configuration">
    /// The application configuration used to bind <see cref="KeycloakConnectionOptions"/> from the
    /// <c>Keycloak:Connection</c> section.
    /// </param>
    /// <param name="configureOptions">
    /// An optional callback that can override or supplement configuration-bound <see cref="KeycloakConnectionOptions"/> values.
    /// Use this when secrets or environment-specific values should be applied in code.
    /// </param>
    /// <returns>The same <paramref name="services"/> instance so registrations can be chained.</returns>
    /// <exception cref="ArgumentNullException">
    /// Thrown when <paramref name="services"/> or <paramref name="configuration"/> is <see langword="null"/>.
    /// </exception>
    /// <remarks>
    /// This method registers validated <see cref="KeycloakConnectionOptions"/>, an HTTP client for token acquisition,
    /// an HTTP client used by the Refit-based admin API clients, <see cref="OpenIdConnectAccessTokenProvider"/>,
    /// and <see cref="IKeycloakConfigurator"/>.
    /// </remarks>
    /// <example>
    /// <code>
    /// services.AddKeycloakClient(builder.Configuration);
    ///
    /// var serviceProvider = services.BuildServiceProvider();
    /// var tokenProvider = serviceProvider.GetRequiredService&lt;OpenIdConnectAccessTokenProvider&gt;();
    /// var configurator = serviceProvider.GetRequiredService&lt;IKeycloakConfigurator&gt;();
    ///
    /// var accessToken = await tokenProvider.GetAuthorizationTokenAsync(CancellationToken.None);
    /// await configurator.ConfigureAsync(configure =&gt;
    /// {
    ///     configure.AddRealm("app", realm =&gt; realm.Enabled());
    /// }, accessToken, CancellationToken.None);
    /// </code>
    /// </example>
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
            .Validate(options => !string.IsNullOrWhiteSpace(options.AdminRealm), "The Keycloak admin realm is required.")
            .Validate(ValidateAuthenticationMode, "Authentication mode configuration is invalid.");

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

    private static bool ValidateAuthenticationMode(KeycloakConnectionOptions options)
    {
        return options.AuthenticationMode switch
        {
            AuthenticationMode.DirectGrant =>
                !string.IsNullOrWhiteSpace(options.AdminUsername) &&
                !string.IsNullOrWhiteSpace(options.AdminPassword),

            AuthenticationMode.ClientCredentials =>
                !string.IsNullOrWhiteSpace(options.ClientId) &&
                !string.IsNullOrWhiteSpace(options.ClientSecret),

            AuthenticationMode.ExternalTokenProvider =>
                options.TokenProvider is not null,

            _ => false
        };
    }
}
