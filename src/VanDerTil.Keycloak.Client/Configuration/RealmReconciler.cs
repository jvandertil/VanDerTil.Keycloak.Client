using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class RealmReconciler
{
    private readonly RefitClientFactory _clientFactory;

    internal RealmReconciler(RefitClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    internal async Task EnsureRealmAsync(RealmConfiguration realm, string accessToken, CancellationToken cancellationToken)
    {
        var realmId = realm.RealmId;
        var realmsClient = _clientFactory.Create<IRealmsResource>(accessToken);
        var realmClient = _clientFactory.Create<IRealmResource>(accessToken);

        var existingRealms = await realmsClient.GetAllAsync(cancellationToken).ConfigureAwait(false);
        var desiredRealm = ConfigurationProjections.CreateRealm(realm);
        var existingRealm = existingRealms.FirstOrDefault(x => string.Equals(x.Realm, realmId, StringComparison.OrdinalIgnoreCase));

        if (existingRealm is null)
        {
            await realmsClient.CreateAsync(desiredRealm, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            await realmClient.UpdateAsync(realmId, ConfigurationProjections.UpdateRealm(existingRealm, desiredRealm, realm), cancellationToken).ConfigureAwait(false);
        }

    }

    internal async Task EnsureRolesAsync(string realmId, IReadOnlyList<string> roles, string accessToken, CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return;
        }

        var rolesClient = _clientFactory.Create<IRolesResource>(accessToken);
        var existingRoles = await rolesClient.GetAllAsync(realmId, cancellationToken).ConfigureAwait(false);
        var existingRoleNames = existingRoles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roles)
        {
            if (!existingRoleNames.Contains(roleName))
            {
                await rolesClient.CreateAsync(realmId, new RoleRepresentation { Name = roleName }, cancellationToken).ConfigureAwait(false);
                existingRoleNames.Add(roleName);
            }
        }
    }
}
