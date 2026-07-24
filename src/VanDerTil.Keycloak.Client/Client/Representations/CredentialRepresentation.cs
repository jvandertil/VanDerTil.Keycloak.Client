namespace VanDerTil.Keycloak.Client.Client.Representations;

public class CredentialRepresentation
{
    public string? Type { get; set; }

    public string? Value { get; set; }

    public bool Temporary { get; set; }
}
