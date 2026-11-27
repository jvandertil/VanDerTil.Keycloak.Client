using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class ClientScopeReconciler
{
    private readonly RefitClientFactory _clientFactory;

    internal ClientScopeReconciler(RefitClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    internal async Task ReconcileAsync(string realm, IReadOnlyList<ClientConfiguration> clients, string token, ResolvedRealmResources resolved, CancellationToken ct)
    {
        var resource = _clientFactory.Create<IClientScopeMappingsResource>(token);
        foreach (var client in clients)
        {
            if (client.RoleScopes.Count == 0 && client.RealmRoleScopes is null)
            {
                continue;
            }
            var clientId = await RoleMappings.ResolveClientIdAsync(_clientFactory, realm, client.ClientId, token, resolved, ct).ConfigureAwait(false);
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
                await RoleMappings.ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveRealmRolesAsync(realm, clientId, roles, ct),
                    roles => resource.AddRealmRolesAsync(realm, clientId, roles, ct)).ConfigureAwait(false);
            }
            foreach (var (roleClient, names) in client.RoleScopes)
            {
                var (roleClientId, desired) = await RoleMappings.ResolveRolesAsync(_clientFactory, realm, roleClient, names, token, resolved, ct).ConfigureAwait(false);
                var current = await resource.GetAsync(realm, clientId, roleClientId, ct).ConfigureAwait(false);
                await RoleMappings.ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveAsync(realm, clientId, roleClientId, roles, ct),
                    roles => resource.AddAsync(realm, clientId, roleClientId, roles, ct)).ConfigureAwait(false);
            }
        }
    }
}
