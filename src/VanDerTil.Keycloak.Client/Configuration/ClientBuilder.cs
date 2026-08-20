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
    public ClientBuilder WithRedirectUri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        _redirectUris.Add(uri);
        return this;
    }

    /// <summary>
    /// Adds an allowed post-logout redirect URI.
    /// </summary>
    /// <param name="uri">The URI that Keycloak may redirect users to after logout completes.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Keycloak stores these values in client attributes rather than in a first-class property, and the configurator
    /// preserves that representation for compatibility with the admin API.
    /// </remarks>
    public ClientBuilder WithPostLogoutRedirectUri(string uri)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(uri);

        _postLogoutRedirectUris.Add(uri);
        return this;
    }

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
        Dictionary<string, string>? attributes = null;

        if (_postLogoutRedirectUris.Count > 0)
        {
            attributes = new Dictionary<string, string>
            {
                ["post.logout.redirect.uris"] = string.Join("##", _postLogoutRedirectUris),
            };
        }

        return new ClientRepresentation
        {
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
