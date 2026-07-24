using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

public enum ClientType
{
    Public,
    Confidential,
}

public sealed class ClientBuilder
{
    private readonly string _clientId;
    private string? _name;
    private string? _secret;
    private ClientType _clientType = ClientType.Public;
    private bool _standardFlow;
    private readonly List<string> _redirectUris = [];
    private readonly List<string> _postLogoutRedirectUris = [];
    private readonly List<string> _roles = [];
    private readonly List<ProtocolMapperBuilder> _protocolMappers = [];

    public ClientBuilder(string clientId)
    {
        _clientId = clientId;
    }

    public ClientBuilder WithName(string name)
    {
        _name = name;
        return this;
    }

    public ClientBuilder Public()
    {
        _clientType = ClientType.Public;
        return this;
    }

    public ClientBuilder Confidential()
    {
        _clientType = ClientType.Confidential;
        return this;
    }

    /// <summary>
    /// Sets the client secret.
    /// </summary>
    public ClientBuilder WithSecret(string secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
        {
            throw new ArgumentException("Value cannot be null or whitespace.", nameof(secret));
        }

        _secret = secret;
        return this;
    }

    public ClientBuilder WithStandardFlow()
    {
        _standardFlow = true;
        return this;
    }

    public ClientBuilder WithRedirectUri(string uri)
    {
        _redirectUris.Add(uri);
        return this;
    }

    public ClientBuilder WithPostLogoutRedirectUri(string uri)
    {
        _postLogoutRedirectUris.Add(uri);
        return this;
    }

    /// <summary>
    /// Defines a client-level role. Client roles are scoped to this client and can be
    /// mapped to token claims via a protocol mapper for use in application authorization.
    /// </summary>
    public ClientBuilder AddRole(string roleName)
    {
        _roles.Add(roleName);
        return this;
    }

    /// <summary>
    /// Adds a protocol mapper that will be configured on this client.
    /// Use <see cref="ProtocolMapperBuilder"/> factory methods to create standard mappers.
    /// </summary>
    public ClientBuilder AddProtocolMapper(ProtocolMapperBuilder mapperBuilder)
    {
        ArgumentNullException.ThrowIfNull(mapperBuilder);

        _protocolMappers.Add(mapperBuilder);
        return this;
    }

    internal ClientType ClientType => _clientType;
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
            PublicClient = _clientType == ClientType.Public,
            StandardFlowEnabled = _standardFlow,
            RedirectUris = _redirectUris.Count > 0 ? [.. _redirectUris] : null,
            Attributes = attributes,
            Enabled = true,
        };
    }
}
