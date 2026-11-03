using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Identifies how Keycloak should treat a client during authentication.
/// </summary>
internal enum ClientType
{
    /// <summary>
    /// A public client that cannot safely keep a client secret, such as a browser or mobile application.
    /// </summary>
    Public,
    /// <summary>
    /// A confidential client that can securely hold a client secret, such as a server-side web application or service.
    /// </summary>
    Confidential,
}

/// <summary>
/// Builds the desired configuration for a Keycloak client within a realm.
/// </summary>
public sealed class ClientBuilder
{
    private bool? _fullScopeAllowed;
    private readonly Dictionary<string, string> _attributes = [];
    internal Dictionary<string, string[]> RoleScopes { get; } = new(StringComparer.Ordinal);
    internal string[]? RealmRoleScopes { get; private set; }

    private readonly string _clientId;
    private string? _name;
    private string? _secret;
    private bool? _publicClient;
    private bool? _standardFlow;
    private bool? _enabled;
    private readonly List<string> _redirectUris = [];
    private readonly List<string> _postLogoutRedirectUris = [];
    private readonly List<string> _roles = [];
    private readonly List<ProtocolMapperBuilder> _protocolMappers = [];

    /// <summary>
    /// Initializes a new instance of the <see cref="ClientBuilder"/> class for the specified client identifier.
    /// </summary>
    /// <param name="clientId">The Keycloak client identifier.</param>
    public ClientBuilder(string clientId)
    {
        _clientId = clientId;
    }

    /// <summary>Controls whether all roles are in this client's role scope.</summary>
    /// <param name="allowed">When <see langword="true"/>, all roles are in scope. When <see langword="false"/>,
    /// role scope mappings restrict which roles are in scope.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Roles in scope can appear in tokens only when the user has those roles and applicable protocol mappers
    /// include them. Restricting the scope does not grant roles to users. Use <see cref="WithClientRoleScope"/>
    /// to configure direct mappings for a role-owning client; linked client scopes can also contribute mappings.
    /// New clients default to <see langword="false"/> when this method is omitted. Existing clients retain
    /// their setting unless it is explicitly specified. Keycloak itself normally defaults new clients to
    /// <see langword="true"/>. Changing this setting can change token claims and application authorization.
    /// </remarks>
    public ClientBuilder WithFullScopeAllowed(bool allowed)
    {
        _fullScopeAllowed = allowed;
        return this;
    }

    /// <summary>Replaces direct role scope mappings for the named role-owning client.</summary>
    /// <param name="clientId">The client that owns the roles, which may be this client or another client.</param>
    /// <param name="roleNames">Roles to include in the direct scope; an empty array removes those mappings.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Other clients' mappings, realm-role mappings, and mappings from linked client scopes are preserved.
    /// These mappings restrict effective roles only when full scope is disabled; they do not assign roles to users.</remarks>
    public ClientBuilder WithClientRoleScope(string clientId, params string[] roleNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(roleNames);
        foreach (var name in roleNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }
        RoleScopes[clientId] = (string[])roleNames.Clone();
        return this;
    }

    /// <summary>Replaces this client's direct realm-role scope mappings.</summary>
    /// <param name="roleNames">Realm roles to include in the direct scope; an empty array removes all direct realm-role mappings.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Omitting this call preserves existing mappings. Client-role mappings and mappings from linked client scopes
    /// are preserved. The roles must exist; mappings do not assign them to users. Full scope must be disabled for
    /// role scope mappings to restrict effective roles.</remarks>
    public ClientBuilder WithRealmRoleScope(params string[] roleNames)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        foreach (var name in roleNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }
        RealmRoleScopes = (string[])roleNames.Clone();
        return this;
    }

    /// <summary>Requires S256 PKCE for authorization code flow.</summary>
    public ClientBuilder WithPkceS256() => WithAttribute("pkce.code.challenge.method", "S256");

    /// <summary>Enables or disables ordinary session-bound refresh tokens for this client.</summary>
    /// <param name="enabled">Whether Keycloak may issue refresh tokens for the client.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>When omitted, Keycloak's default applies to new clients and existing settings are preserved.
    /// This setting does not request offline access or configure realm-wide refresh-token rotation.</remarks>
    public ClientBuilder WithRefreshTokens(bool enabled) => WithAttribute("use.refresh.tokens", enabled ? "true" : "false");

    /// <summary>Sets this client's access-token lifetime.</summary>
    public ClientBuilder WithAccessTokenLifespan(TimeSpan lifetime) => WithDuration("access.token.lifespan", lifetime);

    /// <summary>Sets this client's session idle timeout. Keycloak advances idle time on authentication/refresh.</summary>
    public ClientBuilder WithClientSessionIdleTimeout(TimeSpan lifetime) => WithDuration("client.session.idle.timeout", lifetime);

    /// <summary>Sets this client's maximum session duration.</summary>
    public ClientBuilder WithClientSessionMaxLifespan(TimeSpan lifetime) => WithDuration("client.session.max.lifespan", lifetime);

    private ClientBuilder WithDuration(string key, TimeSpan lifetime)
    {
        if (lifetime <= TimeSpan.Zero || lifetime.TotalSeconds > int.MaxValue || lifetime.Ticks % TimeSpan.TicksPerSecond != 0)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), "Use a positive whole number of seconds within Int32 range.");
        }
        return WithAttribute(key, ((int)lifetime.TotalSeconds).ToString(System.Globalization.CultureInfo.InvariantCulture));
    }

    private ClientBuilder WithAttribute(string key, string value)
    {
        _attributes[key] = value;
        return this;
    }

    /// <summary>
    /// Sets the display name shown for the client in Keycloak.
    /// </summary>
    /// <param name="name">The human-friendly name for the client.</param>
    /// <returns>The current builder.</returns>
    public ClientBuilder WithName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        _name = name;
        return this;
    }

    /// <summary>
    /// Configures the client as a <see cref="ClientType.Public"/> client.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ClientBuilder Public()
    {
        _publicClient = true;
        return this;
    }

    /// <summary>
    /// Configures the client as a <see cref="ClientType.Confidential"/> client.
    /// </summary>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Confidential clients are intended for applications that can protect a client secret.
    /// Pair this with <see cref="WithSecret(string)"/> when the client should authenticate with one.
    /// </remarks>
    public ClientBuilder Confidential()
    {
        _publicClient = false;
        return this;
    }

    /// <summary>
    /// Sets the client secret.
    /// </summary>
    /// <param name="secret">The shared secret the confidential client will use when authenticating to Keycloak.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="secret"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public ClientBuilder WithSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(secret));
        }

        _secret = secret;
        return this;
    }

    /// <summary>
    /// Enables the OpenID Connect standard authorization code flow for the client.
    /// </summary>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Standard flow is commonly required for interactive web applications that sign users in through redirects.
    /// </remarks>
    public ClientBuilder WithStandardFlow()
    {
        _standardFlow = true;
        return this;
    }

    /// <summary>
    /// Marks the client as enabled so it can receive authentication requests.
    /// </summary>
    /// <returns>The current builder.</returns>
    public ClientBuilder Enabled()
    {
        _enabled = true;
        return this;
    }

    /// <summary>
    /// Adds an allowed redirect URI for sign-in responses.
    /// </summary>
    /// <param name="uri">The redirect URI that Keycloak may send users back to after authentication.</param>
    /// <returns>The current builder.</returns>
    public ClientBuilder AddRedirectUri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        _redirectUris.Add(uri);
        return this;
    }

    /// <summary>
    /// Adds an allowed redirect URI for sign-in responses.
    /// </summary>
    /// <param name="uri">The redirect URI that Keycloak may send users back to after authentication.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Use <see cref="AddRedirectUri(string)"/>.</remarks>
    [Obsolete("Use AddRedirectUri instead.")]
    public ClientBuilder WithRedirectUri(string uri) => AddRedirectUri(uri);

    /// <summary>
    /// Adds an allowed post-logout redirect URI.
    /// </summary>
    /// <param name="uri">The URI that Keycloak may redirect users to after logout completes.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Keycloak stores these values in client attributes rather than in a first-class property, and the configurator
    /// preserves that representation for compatibility with the admin API.
    /// </remarks>
    public ClientBuilder AddPostLogoutRedirectUri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        _postLogoutRedirectUris.Add(uri);
        return this;
    }

    /// <summary>
    /// Adds an allowed post-logout redirect URI.
    /// </summary>
    /// <param name="uri">The URI that Keycloak may redirect users to after logout completes.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Use <see cref="AddPostLogoutRedirectUri(string)"/>.</remarks>
    [Obsolete("Use AddPostLogoutRedirectUri instead.")]
    public ClientBuilder WithPostLogoutRedirectUri(string uri) => AddPostLogoutRedirectUri(uri);

    /// <summary>
    /// Defines a client-level role. Client roles are scoped to this client and can be
    /// mapped to token claims via a protocol mapper for use in application authorization.
    /// </summary>
    /// <param name="roleName">The client role name to ensure exists.</param>
    /// <returns>The current builder.</returns>
    public ClientBuilder AddRole(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        _roles.Add(roleName);
        return this;
    }

    /// <summary>
    /// Adds a protocol mapper that will be configured on this client.
    /// Use <see cref="ProtocolMapperBuilder"/> factory methods to create standard mappers.
    /// </summary>
    /// <param name="mapperBuilder">The protocol mapper definition to add to the client.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="mapperBuilder"/> is <see langword="null"/>.</exception>
    /// <remarks>
    /// Protocol mappers control how Keycloak projects data, such as roles, into issued tokens.
    /// </remarks>
    public ClientBuilder AddProtocolMapper(ProtocolMapperBuilder mapperBuilder)
    {
        ArgumentNullException.ThrowIfNull(mapperBuilder);

        _protocolMappers.Add(mapperBuilder);
        return this;
    }

    /// <summary>Adds allowed redirect URIs for sign-in responses.</summary>
    /// <param name="uris">The redirect URIs to add.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Accumulates values with previous singular or bulk calls. An empty array adds nothing;
    /// it does not clear the configured or persisted URI list.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a URI is null, empty, or whitespace.</exception>
    public ClientBuilder AddRedirectUris(params string[] uris)
    {
        ArgumentNullException.ThrowIfNull(uris);
        foreach (var uri in uris)
        {
            AddRedirectUri(uri);
        }
        return this;
    }

    /// <summary>Adds allowed post-logout redirect URIs.</summary>
    /// <param name="uris">The post-logout redirect URIs to add.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Accumulates values with previous singular or bulk calls. An empty array adds nothing;
    /// it does not clear the configured or persisted URI list.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="uris"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a URI is null, empty, or whitespace.</exception>
    public ClientBuilder AddPostLogoutRedirectUris(params string[] uris)
    {
        ArgumentNullException.ThrowIfNull(uris);
        foreach (var uri in uris)
        {
            AddPostLogoutRedirectUri(uri);
        }
        return this;
    }

    /// <summary>Defines client-level roles to ensure exist.</summary>
    /// <param name="roleNames">The client role names to add.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>Accumulates roles with previous singular or bulk calls. An empty array adds nothing.
    /// Omitting a role does not delete its existing Keycloak definition.</remarks>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="roleNames"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a role name is null, empty, or whitespace.</exception>
    public ClientBuilder AddRoles(params string[] roleNames)
    {
        ArgumentNullException.ThrowIfNull(roleNames);
        foreach (var roleName in roleNames)
        {
            AddRole(roleName);
        }
        return this;
    }

    internal bool? PublicClientValue => _publicClient;
    internal bool? StandardFlowEnabledValue => _standardFlow;
    internal bool? EnabledValue => _enabled;
    internal IReadOnlyList<string> Roles => _roles;
    internal IReadOnlyList<ProtocolMapperBuilder> ProtocolMappers => _protocolMappers;

    internal ClientRepresentation Build()
    {
        // Keycloak does not expose postLogoutRedirectUris as a first-class list field on the REST API.
        // Instead it is stored in the attributes bag under "post.logout.redirect.uris" with multiple
        // values concatenated by "##". See https://github.com/keycloak/keycloak/discussions/19087.
        Dictionary<string, string>? attributes = _attributes.Count > 0 ? new(_attributes) : null;

        if (_postLogoutRedirectUris.Count > 0)
        {
            attributes ??= [];
            attributes["post.logout.redirect.uris"] = string.Join("##", _postLogoutRedirectUris);
        }

        return new ClientRepresentation
        {
            FullScopeAllowed = _fullScopeAllowed,
            ClientId = _clientId,
            Name = _name,
            Secret = _secret,
            PublicClient = _publicClient ?? false,
            StandardFlowEnabled = _standardFlow ?? false,
            RedirectUris = _redirectUris.Count > 0 ? [.. _redirectUris] : null,
            Attributes = attributes,
            Enabled = _enabled ?? true,
        };
    }
}
