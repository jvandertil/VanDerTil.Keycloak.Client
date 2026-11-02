namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Applies fluent configuration to a Keycloak instance through the admin API.
/// </summary>
public interface IKeycloakConfigurator
{
    /// <summary>
    /// Creates or updates Keycloak realms, clients, users, roles, and protocol mappers described by the supplied configuration.
    /// </summary>
    /// <param name="configure">
    /// A callback that builds the desired Keycloak state by using <see cref="KeycloakConfigurationBuilder"/> and its nested builders.
    /// </param>
    /// <param name="cancellationToken">A token that can be used to cancel the provisioning operation.</param>
    /// <returns>A task that completes when all requested provisioning operations have finished.</returns>
    /// <remarks>
    /// The configurator is idempotent: resources are created when missing and updated when they already exist.
    /// Updates use merge semantics for supported resources, so values you specify overwrite the matching Keycloak settings
    /// while unspecified values are preserved where possible instead of being cleared wholesale.
    /// </remarks>
    /// <example>
    /// <code>
    /// await configurator.ConfigureAsync(configure =&gt;
    /// {
    ///     configure.AddRealm("app", realm =&gt; realm
    ///         .WithDisplayName("Application")
    ///         .Enabled()
    ///         .AddRole("Admin")
    ///         .AddClient("web", client =&gt; client
    ///             .Public()
    ///             .WithStandardFlow()
    ///             .WithRedirectUri("https://localhost:5001/signin-oidc"))
    ///         .AddUser("alice", user =&gt; user
    ///             .WithEmail("alice@example.com")
    ///             .InRole("Admin")));
    /// }, cancellationToken);
    /// </code>
    /// </example>
    Task ConfigureAsync(Action<KeycloakConfigurationBuilder> configure, CancellationToken cancellationToken);
}
