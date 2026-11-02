using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed partial class KeycloakConfigurator
{
    private async Task ConfigureGroupsAsync(string realm, IReadOnlyList<GroupBuilder> groups, string token, CancellationToken ct)
    {
        var resource = _clientFactory.Create<IGroupsResource>(token);
        foreach (var group in groups)
        {
            var existing = await FindGroupAsync(realm, group.Name, token, ct).ConfigureAwait(false);
            string id;
            if (existing is null)
            {
                using var response = await resource.CreateAsync(realm, new GroupRepresentation { Name = group.Name }, ct).ConfigureAwait(false);
                id = ExtractInternalIdFromLocationHeader(response, $"group '{group.Name}'");
            }
            else
            {
                id = existing.Id ?? throw new InvalidOperationException($"Group '{group.Name}' has no id.");
            }

            foreach (var (clientId, names) in group.ClientRoles)
            {
                var (roleClientId, desired) = await ResolveRolesAsync(realm, clientId, names, token, ct).ConfigureAwait(false);
                var current = await resource.GetRolesAsync(realm, id, roleClientId, ct).ConfigureAwait(false);
                await ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveRolesAsync(realm, id, roleClientId, roles, ct),
                    roles => resource.AddRolesAsync(realm, id, roleClientId, roles, ct)).ConfigureAwait(false);
            }
        }
    }

    private async Task<string> FindGroupIdAsync(string realm, string name, string token, CancellationToken ct)
    {
        return (await FindGroupAsync(realm, name, token, ct).ConfigureAwait(false))?.Id
            ?? throw new InvalidOperationException($"Top-level group '{name}' not found in realm '{realm}'.");
    }

    private async Task<GroupRepresentation?> FindGroupAsync(string realm, string name, string token, CancellationToken ct)
    {
        using var response = await _clientFactory.Create<IGroupsResource>(token).GetByPathAsync(realm, name, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);
        return response.Content ?? throw new InvalidOperationException($"Keycloak returned no group for '{name}'.");
    }

    private async Task ConfigureClientScopesAsync(string realm, IReadOnlyList<ClientBuilder> clients, string token, CancellationToken ct)
    {
        var resource = _clientFactory.Create<IClientScopeMappingsResource>(token);
        foreach (var client in clients)
        {
            if (client.RoleScopes.Count == 0 && client.RealmRoleScopes is null)
            {
                continue;
            }
            var allClients = await _clientFactory.Create<IClientsResource>(token).GetAllAsync(realm, ct).ConfigureAwait(false);
            var clientId = allClients.Single(c => c.ClientId == client.Build().ClientId).Id!;
            if (client.RealmRoleScopes is { } realmRoleNames)
            {
                var available = await _clientFactory.Create<IRolesResource>(token).GetAllAsync(realm, ct).ConfigureAwait(false);
                var desired = new List<RoleRepresentation>();
                foreach (var name in realmRoleNames.Distinct(StringComparer.Ordinal))
                {
                    desired.Add(available.SingleOrDefault(r => r.Name == name)
                        ?? throw new InvalidOperationException($"Realm role '{name}' not found in realm '{realm}'."));
                }
                var current = await resource.GetRealmRolesAsync(realm, clientId, ct).ConfigureAwait(false);
                await ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveRealmRolesAsync(realm, clientId, roles, ct),
                    roles => resource.AddRealmRolesAsync(realm, clientId, roles, ct)).ConfigureAwait(false);
            }
            foreach (var (roleClient, names) in client.RoleScopes)
            {
                var (roleClientId, desired) = await ResolveRolesAsync(realm, roleClient, names, token, ct).ConfigureAwait(false);
                var current = await resource.GetAsync(realm, clientId, roleClientId, ct).ConfigureAwait(false);
                await ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveAsync(realm, clientId, roleClientId, roles, ct),
                    roles => resource.AddAsync(realm, clientId, roleClientId, roles, ct)).ConfigureAwait(false);
            }
        }
    }

    private async Task<(string Id, List<RoleRepresentation> Roles)> ResolveRolesAsync(string realm, string clientId, string[] names, string token, CancellationToken ct)
    {
        var clients = await _clientFactory.Create<IClientsResource>(token).GetAllAsync(realm, ct).ConfigureAwait(false);
        var id = clients.SingleOrDefault(c => c.ClientId == clientId)?.Id
            ?? throw new InvalidOperationException($"Client '{clientId}' not found in realm '{realm}'.");
        var available = await _clientFactory.Create<IClientRolesResource>(token).GetAllAsync(realm, id, ct).ConfigureAwait(false);
        var desired = new List<RoleRepresentation>();
        foreach (var name in names.Distinct(StringComparer.Ordinal))
        {
            desired.Add(available.SingleOrDefault(r => r.Name == name)
                ?? throw new InvalidOperationException($"Role '{name}' not found on client '{clientId}'."));
        }
        return (id, desired);
    }

    private static async Task ReconcileMappingsAsync(List<RoleRepresentation> current, List<RoleRepresentation> desired,
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
}
