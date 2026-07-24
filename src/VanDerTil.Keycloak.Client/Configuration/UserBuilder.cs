using Microsoft.Extensions.Configuration;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

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

    public UserBuilder WithFirstName(string? firstName)
    {
        _firstName = firstName;
        return this;
    }

    public UserBuilder WithLastName(string? lastName)
    {
        _lastName = lastName;
        return this;
    }

    public UserBuilder WithEmail(string email)
    {
        _email = email;
        return this;
    }

    public UserBuilder WithPassword(string password)
    {
        _password = password;
        return this;
    }

    public UserBuilder WithTemporaryPassword(string password)
    {
        _temporaryPassword = password;
        return this;
    }

    public UserBuilder InRole(string roleName)
    {
        _roles.Add(roleName);
        return this;
    }

    public UserBuilder InClientRole(string clientId, string roleName)
    {
        _clientRoles.Add((clientId, roleName));
        return this;
    }

    /// <summary>
    /// Overlays environment-specific values from a configuration section.
    /// Recognised keys: <c>TemporaryPassword</c>.
    /// Values in the section take precedence over anything already set in code.
    /// </summary>
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
