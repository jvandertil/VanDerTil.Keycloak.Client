namespace VanDerTil.Keycloak.Client.Configuration;

/// <summary>Defines a top-level group and its managed client-role mappings.</summary>
public sealed class GroupBuilder
{
    internal GroupBuilder(string name) => Name = name;
    internal string Name { get; }
    internal Dictionary<string, string[]> ClientRoles { get; } = new(StringComparer.Ordinal);

    /// <summary>Replaces this group's direct mappings for the named client. Empty removes all of that client's mappings.
    /// Realm roles, other clients' mappings, and group members are preserved.</summary>
    public GroupBuilder WithClientRoles(string clientId, params string[] roleNames)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientId);
        ArgumentNullException.ThrowIfNull(roleNames);
        foreach (var name in roleNames)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(name);
        }
        ClientRoles[clientId] = (string[])roleNames.Clone();
        return this;
    }
}