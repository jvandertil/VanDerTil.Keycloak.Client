using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class GroupReconciler
{
    private readonly RefitClientFactory _clientFactory;

    internal GroupReconciler(RefitClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    internal async Task<ResolvedRealmResources> ReconcileAsync(string realm, IReadOnlyList<GroupConfiguration> groups, string token, ResolvedRealmResources resolved, CancellationToken ct)
    {
        var resource = _clientFactory.Create<IGroupsResource>(token);
        foreach (var group in groups)
        {
            var existing = await GroupLookup.FindGroupAsync(_clientFactory, realm, group.Name, token, ct).ConfigureAwait(false);
            string id;
            if (existing is null)
            {
                using var response = await resource.CreateAsync(realm, new GroupRepresentation { Name = group.Name }, ct).ConfigureAwait(false);
                id = ResourceLocation.ExtractInternalIdFromLocationHeader(response, $"group '{group.Name}'");
            }
            else
            {
                id = existing.Id ?? throw new InvalidOperationException($"Group '{group.Name}' has no id.");
            }

            resolved = resolved with { Groups = resolved.Groups.SetItem(group.Name, id) };

            foreach (var (clientId, names) in group.ClientRoles)
            {
                var (roleClientId, desired) = await RoleMappings.ResolveRolesAsync(_clientFactory, realm, clientId, names, token, resolved, ct).ConfigureAwait(false);
                var current = await resource.GetRolesAsync(realm, id, roleClientId, ct).ConfigureAwait(false);
                await RoleMappings.ReconcileMappingsAsync(current, desired,
                    roles => resource.RemoveRolesAsync(realm, id, roleClientId, roles, ct),
                    roles => resource.AddRolesAsync(realm, id, roleClientId, roles, ct)).ConfigureAwait(false);
            }
        }
        return resolved;
    }
}
