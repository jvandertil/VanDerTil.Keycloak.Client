using Microsoft.Extensions.Configuration;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>
/// Builds the desired configuration for a Keycloak user within a realm.
/// </summary>
public sealed class UserBuilder
{
    private readonly string _username;
    private string? _firstName;
    private string? _lastName;
    private string? _email;
    private string? _temporaryPassword;
    private string? _password;
    private readonly List<string> _roles = [];
    private readonly List<(string ClientId, string RoleName)> _clientRoles = [];

    internal UserBuilder(string username)
    {
        _username = username;
    }

    /// <summary>
    /// Sets the user's first name.
    /// </summary>
    /// <param name="firstName">The first name to store for the user.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder WithFirstName(string? firstName)
    {
        _firstName = firstName;
        return this;
    }

    /// <summary>
    /// Sets the user's last name.
    /// </summary>
    /// <param name="lastName">The last name to store for the user.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder WithLastName(string? lastName)
    {
        _lastName = lastName;
        return this;
    }

    /// <summary>
    /// Sets the user's email address.
    /// </summary>
    /// <param name="email">The email address Keycloak should store for the user.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder WithEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        _email = email;
        return this;
    }

    /// <summary>
    /// Sets a non-temporary password for the user.
    /// </summary>
    /// <param name="password">The password the user can continue using without an immediate reset.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder WithPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _password = password;
        return this;
    }

    /// <summary>
    /// Sets a temporary password for the user.
    /// </summary>
    /// <param name="password">The password the user must change at first sign-in.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder WithTemporaryPassword(string password)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(password);

        _temporaryPassword = password;
        return this;
    }

    /// <summary>
    /// Assigns a realm-level role to the user.
    /// </summary>
    /// <param name="roleName">The name of the realm role to assign.</param>
    /// <returns>The current builder.</returns>
    public UserBuilder InRole(string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        _roles.Add(roleName);
        return this;
    }

    /// <summary>
    /// Assigns a client-level role to the user.
    /// </summary>
    /// <param name="clientId">The client that owns the role.</param>
    /// <param name="roleName">The client role name to assign.</param>
    /// <returns>The current builder.</returns>
    /// <remarks>
    /// Use this when application authorization is based on client roles rather than realm roles.
    /// </remarks>
    public UserBuilder InClientRole(string clientId, string roleName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentException.ThrowIfNullOrWhiteSpace(roleName);

        _clientRoles.Add((clientId, roleName));
        return this;
    }

    /// <summary>
    /// Overlays environment-specific values from a configuration section.
    /// Recognised keys: <c>TemporaryPassword</c>.
    /// Values in the section take precedence over anything already set in code.
    /// </summary>
    /// <param name="section">The configuration section containing user-specific overrides.</param>
    /// <returns>The current builder.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="section"/> is <see langword="null"/>.</exception>
    public UserBuilder WithConfiguration(IConfigurationSection section)
    {
        ArgumentNullException.ThrowIfNull(section);

        var temporaryPassword = section["TemporaryPassword"];
        if (!string.IsNullOrWhiteSpace(temporaryPassword))
        {
            _temporaryPassword = temporaryPassword;
        }

        var password = section["Password"];
        if (!string.IsNullOrWhiteSpace(password))
        {
            _password = password;
        }

        return this;
    }

    internal UserRepresentation Build()
    {
        var user = new UserRepresentation
        {
            Username = _username,
            FirstName = _firstName,
            LastName = _lastName,
            Email = _email,
            Enabled = true,
        };

        if (_temporaryPassword is not null)
        {
            user.Credentials =
            [
                new CredentialRepresentation
                {
                    Type = "password",
                    Value = _temporaryPassword,
                    Temporary = true,
                }
            ];
        }

        if (_password is not null)
        {
            user.Credentials ??= [];
            user.Credentials.Add(new CredentialRepresentation
            {
                Type = "password",
                Value = _password,
                Temporary = false
            });
        }

        return user;
    }

    internal IReadOnlyList<string> Roles => _roles;
    internal IReadOnlyList<(string ClientId, string RoleName)> ClientRoles => _clientRoles;
}
