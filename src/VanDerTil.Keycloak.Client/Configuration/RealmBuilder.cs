using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Builds the desired configuration for a single Keycloak realm.
/// </summary>
public sealed class RealmBuilder
{
    private readonly string _realmId;
    private string? _displayName;
    private bool? _enabled;
    private readonly List<ClientBuilder> _clients = [];
    private readonly List<UserBuilder> _users = [];
    private readonly List<string> _roles = [];

    internal RealmBuilder(string realmId)
    {
        _realmId = realmId;
    }

    /// <summary>
    /// Sets the display name shown in the Keycloak administration UI for the realm.
    /// </summary>
    /// <param name="displayName">The human-friendly name to show for the realm.</param>
    /// <returns>The current builder.</returns>
    public RealmBuilder WithDisplayName(string displayName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(displayName);

        _displayName = displayName;
        return this;
    }

    /// <summary>
    /// Marks the realm as enabled so it can accept authentication and administration requests.
    /// </summary>
    /// <returns>The current builder.</returns>
    public RealmBuilder Enabled()
    {
        _enabled = true;
        return this;
    }

    /// <summary>
    /// Adds or updates a client within the realm.
    /// </summary>
    /// <param name="clientId">The Keycloak client identifier.</param>
    /// <param name="configure">A callback that configures client type, roles, redirect URIs, and protocol mappers.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    public RealmBuilder AddClient(string clientId, Action<ClientBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new ClientBuilder(clientId);
        configure(builder);
        _clients.Add(builder);
        return this;
    }

    /// <summary>
    /// Adds or updates a user within the realm.
    /// </summary>
    /// <param name="username">The Keycloak username.</param>
    /// <param name="configure">A callback that configures profile data, credentials, and role assignments.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="configure"/> is <see langword="null"/>.</exception>
    public RealmBuilder AddUser(string username, Action<UserBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(configure);

        var builder = new UserBuilder(username);
        configure(builder);
        _users.Add(builder);
        return this;
    }

    /// <summary>
    /// Ensures that a realm-level role exists.
    /// </summary>
    /// <param name="roleName">The name of the realm role to provision.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Realm roles can later be assigned to users with <see cref="UserBuilder.InRole(string)"/>.
    /// </remarks>
    public RealmBuilder AddRole(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        _roles.Add(roleName);
        return this;
    }

    internal bool? EnabledValue => _enabled;

    internal RealmRepresentation Build()
    {
        return new RealmRepresentation
        {
            Id = _realmId,
            Realm = _realmId,
            DisplayName = _displayName,
            Enabled = _enabled ?? false,
        };
    }

    internal IReadOnlyList<ClientBuilder> Clients => _clients;
    internal IReadOnlyList<UserBuilder> Users => _users;
    internal IReadOnlyList<string> Roles => _roles;
    internal string RealmId => _realmId;
}
