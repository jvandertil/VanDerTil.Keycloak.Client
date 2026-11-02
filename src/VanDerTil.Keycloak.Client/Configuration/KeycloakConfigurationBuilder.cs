namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Builds the desired top-level Keycloak configuration to provision.
/// </summary>
public sealed class KeycloakConfigurationBuilder
{
    private readonly List<RealmBuilder> _realms = [];

    /// <summary>
    /// Adds a realm definition to the configuration.
    /// </summary>
    /// <param name="realmId">The realm identifier used by Keycloak for the realm.</param>
    /// <param name="configure">A callback that configures the realm's display settings, roles, clients, and users.</param>
    /// <returns>The current builder so additional realms can be added fluently.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Use this as the entry point for every realm you want the configurator to reconcile against Keycloak.
    /// </remarks>
    public KeycloakConfigurationBuilder AddRealm(string realmId, Action<RealmBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(realmId);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new RealmBuilder(realmId);
        configure(builder);
        _realms.Add(builder);
        return this;
    }

    internal IReadOnlyList<RealmBuilder> Realms => _realms;
}
