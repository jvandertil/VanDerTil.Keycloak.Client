namespace VanDerTil.Keycloak.Client.Configuration;

public sealed class KeycloakConfigurationBuilder
{
    private readonly List<RealmBuilder> _realms = [];

    public KeycloakConfigurationBuilder AddRealm(string realmId, Action<RealmBuilder> configure)
    {
        var builder = new RealmBuilder(realmId);
        configure(builder);
        _realms.Add(builder);
        return this;
    }

    internal IReadOnlyList<RealmBuilder> Realms => _realms;
}
