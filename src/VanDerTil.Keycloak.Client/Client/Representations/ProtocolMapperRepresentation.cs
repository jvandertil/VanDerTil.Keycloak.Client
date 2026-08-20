using System.Text.Json.Serialization;

namespace VanDerTil.Keycloak.Client.Client.Representations;

internal class ProtocolMapperRepresentation
{
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Id { get; set; }

    public string? Name { get; set; }

    public string? Protocol { get; set; }

    public string? ProtocolMapper { get; set; }

    public bool ConsentRequired { get; set; }

    public Dictionary<string, string>? Config { get; set; }
}
