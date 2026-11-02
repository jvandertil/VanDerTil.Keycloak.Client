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
    private int? _ssoSessionIdleTimeout;
    private int? _ssoSessionMaxLifespan;

    private bool? _revokeRefreshToken;
    private int? _refreshTokenMaxReuse;
    internal List<GroupBuilder> Groups { get; } = [];

    private readonly List<ClientBuilder> _clients = [];
    private readonly List<UserBuilder> _users = [];
    private readonly List<string> _roles = [];

    internal RealmBuilder(string realmId)
    {
        _realmId = realmId;
    }

    /// <summary>Sets the realm SSO idle ceiling. Client idle limits cannot exceed this limit.</summary>
    public RealmBuilder WithSsoSessionIdleTimeout(TimeSpan lifetime)
    {
        _ssoSessionIdleTimeout = ToSeconds(lifetime);
        return this;
    }

    /// <summary>Sets the realm SSO maximum lifespan ceiling for client sessions.</summary>
    public RealmBuilder WithSsoSessionMaxLifespan(TimeSpan lifetime)
    {
        _ssoSessionMaxLifespan = ToSeconds(lifetime);
        return this;
    }

    private static int ToSeconds(TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime.TotalSeconds > int.MaxValue || lifetime.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Use a positive whole number of seconds within Int32 range.");
        }
        return (int)lifetime.TotalSeconds;
    }

    /// <summary>Defines a top-level group. Existing members and unrelated role mappings are preserved.</summary>
    public RealmBuilder AddGroup(string name, Action<GroupBuilder> configure)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        if (name.Contains('/', StringComparison.Ordinal))
        {
            throw new ArgumentException("Use a top-level group name without '/'.", nameof(name));
        }
        ArgumentNullException.ThrowIfNull(configure);
        var group = new GroupBuilder(name);
        configure(group);
        Groups.Add(group);
        return this;
    }

    /// <summary>Sets realm-wide refresh-token rotation. This affects every client in the realm.</summary>
    public RealmBuilder WithRefreshTokenRotation(bool enabled, int maxReuse = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maxReuse);
        _revokeRefreshToken = enabled;
        _refreshTokenMaxReuse = maxReuse;
        return this;
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
            SsoSessionIdleTimeout = _ssoSessionIdleTimeout,
            SsoSessionMaxLifespan = _ssoSessionMaxLifespan,
            RevokeRefreshToken = _revokeRefreshToken,
            RefreshTokenMaxReuse = _refreshTokenMaxReuse,
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