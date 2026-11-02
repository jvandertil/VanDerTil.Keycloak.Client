using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Builds a Keycloak protocol mapper configuration. Use the static factory methods to create
/// pre-configured builders for common mapping scenarios.
/// </summary>
public sealed class ProtocolMapperBuilder
{
    private readonly string _name;
    private readonly string _protocolMapper;
    private readonly Dictionary<string, string> _config = [];

    private ProtocolMapperBuilder(string name, string protocolMapper)
    {
        _name = name;
        _protocolMapper = protocolMapper;
    }

    /// <summary>
    /// Sets a configuration entry on the mapper.
    /// </summary>
    /// <param name="key">The mapper configuration key to set.</param>
    /// <param name="value">The value to store for the configuration key.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentException">Thrown when <paramref name="key"/> is <see langword="null"/>, empty, or whitespace.</exception>
    public ProtocolMapperBuilder WithConfig(string key, string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        _config[key] = value;
        return this;
    }

    /// <summary>
    /// Creates a mapper that maps a user's client roles to a token claim.
    /// Use this to make Keycloak client roles available as a standard claim so that
    /// ASP.NET Core role-based authorization (<c>[Authorize(Roles = "...")]</c>) works out of the box.
    /// </summary>
    /// <param name="name">The display name of the mapper as shown in Keycloak.</param>
    /// <param name="claimName">The token claim name to populate (e.g. <c>"roles"</c>).</param>
    /// <returns>A builder preconfigured for the standard user client roles mapper.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when <paramref name="name"/> or <paramref name="claimName"/> is <see langword="null"/>, empty, or whitespace.
    /// </exception>
    public static ProtocolMapperBuilder UserClientRolesToClaim(string name, string claimName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentException.ThrowIfNullOrWhiteSpace(claimName);

        // oidc-usermodel-client-role-mapper reads the client roles assigned to the user
        // and injects them as a multivalued string claim into the access, ID, and userinfo tokens.
        return new ProtocolMapperBuilder(name, "oidc-usermodel-client-role-mapper")
            .WithConfig("multivalued", "true")
            .WithConfig("userinfo.token.claim", "true")
            .WithConfig("id.token.claim", "true")
            .WithConfig("access.token.claim", "true")
            .WithConfig("claim.name", claimName)
            .WithConfig("jsonType.label", "String");
    }

    internal ProtocolMapperRepresentation Build()
    {
        return new ProtocolMapperRepresentation
        {
            Name = _name,
            Protocol = "openid-connect",
            ProtocolMapper = _protocolMapper,
            ConsentRequired = false,
            Config = _config.Count > 0 ? new Dictionary<string, string>(_config) : null,
        };
    }
}
