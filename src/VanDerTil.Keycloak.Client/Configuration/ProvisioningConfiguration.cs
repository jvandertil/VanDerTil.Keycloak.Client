using System.Linq;

namespace VanDerTil.Keycloak.Client.Configuration;

// Detached desired state. These types deliberately do not generate ToString methods for secrets.
internal sealed class ProvisioningConfiguration
{
    internal required IReadOnlyList<RealmConfiguration> Realms { get; init; }
}
internal sealed class RealmConfiguration
{
    internal required string RealmId { get; init; }
    internal string? DisplayName { get; init; }
    internal bool? Enabled { get; init; }
    internal int? SsoSessionIdleTimeout { get; init; }
    internal int? SsoSessionMaxLifespan { get; init; }
    internal bool? RevokeRefreshToken { get; init; }
    internal int? RefreshTokenMaxReuse { get; init; }
    internal required IReadOnlyList<ClientConfiguration> Clients { get; init; }
    internal required IReadOnlyList<UserConfiguration> Users { get; init; }
    internal required IReadOnlyList<GroupConfiguration> Groups { get; init; }
    internal required IReadOnlyList<string> Roles { get; init; }
}
internal sealed class ClientConfiguration
{
    internal required string ClientId { get; init; }
    internal string? Name { get; init; }
    internal string? Secret { get; init; }
    internal bool? PublicClient { get; init; }
    internal bool? StandardFlowEnabled { get; init; }
    internal bool? Enabled { get; init; }
    internal bool? FullScopeAllowed { get; init; }
    internal required IReadOnlyList<string> RedirectUris { get; init; }
    internal required IReadOnlyList<string> PostLogoutRedirectUris { get; init; }
    internal required IReadOnlyDictionary<string, string> Attributes { get; init; }
    internal required IReadOnlyDictionary<string, IReadOnlyList<string>> RoleScopes { get; init; }
    internal IReadOnlyList<string>? RealmRoleScopes { get; init; }
    internal required IReadOnlyList<string> Roles { get; init; }
    internal required IReadOnlyList<ProtocolMapperConfiguration> ProtocolMappers { get; init; }
}
internal sealed class UserConfiguration
{
    internal required string Username { get; init; }
    internal string? FirstName { get; init; }
    internal string? LastName { get; init; }
    internal string? Email { get; init; }
    internal string? TemporaryPassword { get; init; }
    internal string? Password { get; init; }
    internal required IReadOnlyList<string> Roles { get; init; }
    internal required IReadOnlyList<(string ClientId, string RoleName)> ClientRoles { get; init; }
    internal required IReadOnlyList<string> Groups { get; init; }
}
internal sealed class GroupConfiguration
{
    internal required string Name { get; init; }
    internal required IReadOnlyDictionary<string, IReadOnlyList<string>> ClientRoles { get; init; }
}
internal sealed class ProtocolMapperConfiguration
{
    internal required string Name { get; init; }
    internal required string ProtocolMapper { get; init; }
    internal required IReadOnlyDictionary<string, string> Config { get; init; }
}
internal static class ConfigurationCopies
{
    internal static IReadOnlyList<T> List<T>(IEnumerable<T> values) => Array.AsReadOnly(values.ToArray());
    internal static IReadOnlyDictionary<string, string> Dictionary(Dictionary<string, string> values) =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, string>(new Dictionary<string, string>(values, StringComparer.Ordinal));
    internal static IReadOnlyDictionary<string, IReadOnlyList<string>> Mappings(Dictionary<string, string[]> values) =>
        new System.Collections.ObjectModel.ReadOnlyDictionary<string, IReadOnlyList<string>>(
            values.ToDictionary(pair => pair.Key, pair => List(pair.Value), StringComparer.Ordinal));
}
