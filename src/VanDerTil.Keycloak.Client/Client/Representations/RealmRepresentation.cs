namespace VanDerTil.Keycloak.Client.Client.Representations;

internal class RealmRepresentation
{
    public string? Id { get; set; }

    public string? Realm { get; set; }

    public string? DisplayName { get; set; }

    public bool Enabled { get; set; }
}
