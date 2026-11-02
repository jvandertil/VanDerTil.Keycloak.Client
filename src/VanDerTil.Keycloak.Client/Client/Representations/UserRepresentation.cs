namespace VanDerTil.Keycloak.Client.Client.Representations;

internal class UserRepresentation
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement>? AdditionalProperties { get; set; }

    public string? Id { get; set; }

    public string? Username { get; set; }

    public string? FirstName { get; set; }

    public string? LastName { get; set; }

    public string? Email { get; set; }

    public bool Enabled { get; set; } = true;

    public List<CredentialRepresentation>? Credentials { get; set; }
}