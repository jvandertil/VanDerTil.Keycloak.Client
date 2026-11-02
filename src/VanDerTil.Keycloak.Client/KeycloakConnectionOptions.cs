namespace VanDerTil.Keycloak.Client;

/// <summary>
/// Authentication mode for connecting to the Keycloak admin API.
/// </summary>
public enum AuthenticationMode
{
    /// <summary>
    /// Resource Owner Password Credentials flow (direct grant).
    /// Uses username and password to obtain a token via the admin-cli client.
    /// Suitable for development but not recommended for production.
    /// </summary>
    DirectGrant = 0,

    /// <summary>
    /// Client Credentials flow.
    /// Uses a service account client with client_id and client_secret.
    /// Recommended for production service-to-service authentication.
    /// </summary>
    ClientCredentials = 1,

    /// <summary>
    /// External token provider.
    /// Allows the consumer to provide a custom token acquisition function.
    /// Useful for integration with enterprise identity systems, managed identities, or custom flows.
    /// </summary>
    ExternalTokenProvider = 2
}

/// <summary>
/// Configuration values used to connect to Keycloak and obtain an admin access token.
/// </summary>
public sealed class KeycloakConnectionOptions
{
    /// <summary>
    /// The configuration section name that <see cref="KeycloakServiceCollectionExtensions.AddKeycloakClient(Microsoft.Extensions.DependencyInjection.IServiceCollection, Microsoft.Extensions.Configuration.IConfiguration, Action{KeycloakConnectionOptions}?)"/>
    /// binds from.
    /// </summary>
    public const string SectionName = "Keycloak:Connection";

    /// <summary>
    /// Gets or sets the absolute base URL of the Keycloak instance, such as <c>https://keycloak.example.com</c>.
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:43000";

    /// <summary>
    /// Gets or sets the realm that contains the administrative account or service account used for token acquisition, such as <c>master</c>.
    /// </summary>
    public string AdminRealm { get; set; } = "master";

    /// <summary>
    /// Gets or sets the authentication mode to use when obtaining access tokens.
    /// Default is <see cref="AuthenticationMode.DirectGrant"/> for backward compatibility,
    /// but <see cref="AuthenticationMode.ClientCredentials"/> is recommended for production.
    /// </summary>
    public AuthenticationMode AuthenticationMode { get; set; } = AuthenticationMode.DirectGrant;

    // Direct Grant (Resource Owner Password Credentials) properties
    /// <summary>
    /// Gets or sets the username of the administrative account for DirectGrant authentication mode.
    /// Only required when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.DirectGrant"/>.
    /// </summary>
    public string? AdminUsername { get; set; }

    /// <summary>
    /// Gets or sets the password of the administrative account for DirectGrant authentication mode.
    /// Only required when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.DirectGrant"/>.
    /// </summary>
    public string? AdminPassword { get; set; }

    // Client Credentials properties
    /// <summary>
    /// Gets or sets the client ID for ClientCredentials authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.ClientCredentials"/>.
    /// The client should have "Service Account Enabled" and appropriate admin roles assigned.
    /// </summary>
    public string? ClientId { get; set; }

    /// <summary>
    /// Gets or sets the client secret for ClientCredentials authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.ClientCredentials"/>.
    /// </summary>
    public string? ClientSecret { get; set; }

    // External Token Provider
    /// <summary>
    /// Gets or sets the custom token provider function for ExternalTokenProvider authentication mode.
    /// Required when <see cref="AuthenticationMode"/> is <see cref="AuthenticationMode.ExternalTokenProvider"/>.
    /// This function should return a valid access token for the Keycloak admin API.
    /// </summary>
    public Func<CancellationToken, Task<string>>? TokenProvider { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether calling code should provision a development seed user.
    /// </summary>
    /// <remarks>
    /// This library exposes the option so application code can make environment-specific provisioning decisions.
    /// The library itself does not automatically create a seed user based on this flag.
    /// </remarks>
    public bool SeedTestUser { get; set; }

}
