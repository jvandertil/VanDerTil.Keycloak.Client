using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

public sealed class RealmBuilder
{
    private readonly string _realmId;
    private string? _displayName;
    private bool _enabled;
    private readonly List<ClientBuilder> _clients = [];
    private readonly List<UserBuilder> _users = [];
    private readonly List<string> _roles = [];

    internal RealmBuilder(string realmId)
    {
        _realmId = realmId;
    }

    public RealmBuilder WithDisplayName(string displayName)
    {
        _displayName = displayName;
        return this;
    }

    public RealmBuilder Enabled()
    {
        _enabled = true;
        return this;
    }

    public RealmBuilder AddClient(string clientId, Action<ClientBuilder> configure)
    {
        var builder = new ClientBuilder(clientId);
        configure(builder);
        _clients.Add(builder);
        return this;
    }

    public RealmBuilder AddUser(string username, Action<UserBuilder> configure)
    {
        var builder = new UserBuilder(username);
        configure(builder);
        _users.Add(builder);
        return this;
    }

    public RealmBuilder AddRole(string roleName)
    {
        _roles.Add(roleName);
        return this;
    }

    internal RealmRepresentation Build()
    {
        return new RealmRepresentation
        {
            Id = _realmId,
            Realm = _realmId,
            DisplayName = _displayName,
            Enabled = _enabled,
        };
    }

    internal IReadOnlyList<ClientBuilder> Clients => _clients;
    internal IReadOnlyList<UserBuilder> Users => _users;
    internal IReadOnlyList<string> Roles => _roles;
    internal string RealmId => _realmId;
}
