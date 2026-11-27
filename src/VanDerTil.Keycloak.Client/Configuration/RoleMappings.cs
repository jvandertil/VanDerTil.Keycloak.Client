using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal static class RoleMappings
{
    internal static async Task<(string Id, List<RoleRepresentation> Roles)> ResolveRolesAsync(
        RefitClientFactory clientFactory, string realm, string clientId, IReadOnlyList<string> names,
        string token, ResolvedRealmResources resolved, CancellationToken ct)
    {
        var id = await ResolveClientIdAsync(clientFactory, realm, clientId, token, resolved, ct).ConfigureAwait(false);
        var available = await clientFactory.Create<IClientRolesResource>(token).GetAllAsync(realm, id, ct).ConfigureAwait(false);
        var desired = new List<RoleRepresentation>();
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            desired.Add(available.SingleOrDefault(r => r.Name == name)
                ?? throw new InvalidOperationException($"Role '{name}' not found on client '{clientId}'."));
        }
        return (id, desired);
    }

    internal static async Task ReconcileMappingsAsync(List<RoleRepresentation> current, List<RoleRepresentation> desired,
        Func<List<RoleRepresentation>, Task> remove, Func<List<RoleRepresentation>, Task> add)
    {
        var removed = current.Where(r => !desired.Any(d => d.Id == r.Id)).ToList();
        var added = desired.Where(r => !current.Any(c => c.Id == r.Id)).ToList();
        if (removed.Count > 0)
        {
            await remove(removed).ConfigureAwait(false);
        }
        if (added.Count > 0)
        {
            await add(added).ConfigureAwait(false);
        }
    }

    internal static async Task<string> ResolveClientIdAsync(
        RefitClientFactory clientFactory, string realm, string clientId, string token,
        ResolvedRealmResources resolved, CancellationToken ct)
    {
        if (resolved.Clients.TryGetValue(clientId, out var id))
        {
            return id;
        }
        var clients = await clientFactory.Create<IClientsResource>(token).GetAllAsync(realm, ct).ConfigureAwait(false);
        return clients.SingleOrDefault(c => c.ClientId == clientId)?.Id
            ?? throw new InvalidOperationException($"Client '{clientId}' not found in realm '{realm}'.");
    }
}
