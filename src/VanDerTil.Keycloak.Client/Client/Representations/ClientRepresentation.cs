using System.Text.Json.Serialization;

namespace VanDerTil.Keycloak.Client.Client.Representations;

internal class ClientRepresentation
{
    public string? Id { get; set; }

    public string? ClientId { get; set; }

    public string? Name { get; set; }

    public string? Secret { get; set; }

    public bool PublicClient { get; set; }

    public bool StandardFlowEnabled { get; set; }

    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public List<string>? RedirectUris { get; set; }

    /// <summary>
    /// Client-level attributes. Keycloak stores some settings here that are not top-level fields,
    /// for example post-logout redirect URIs are stored as <c>post.logout.redirect.uris</c>
    /// with multiple values joined by <c>##</c>.
    /// </summary>
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public Dictionary<string, string>? Attributes { get; set; }

    public bool Enabled { get; set; } = true;
}
