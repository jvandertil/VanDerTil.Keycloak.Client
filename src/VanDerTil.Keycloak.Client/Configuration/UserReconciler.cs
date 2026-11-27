using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class UserReconciler
{
    private readonly RefitClientFactory _clientFactory;

    internal UserReconciler(RefitClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    internal async Task ReconcileAsync(
        string realmId, IReadOnlyList<UserConfiguration> users, string accessToken, ResolvedRealmResources resolved, CancellationToken cancellationToken)
    {
        if (users.Count == 0)
        {
            return;
        }

        var usersResource = _clientFactory.Create<IUsersResource>(accessToken);
        var rolesResource = _clientFactory.Create<IRolesResource>(accessToken);
        var clientsResource = _clientFactory.Create<IClientsResource>(accessToken);
        var clientRolesResource = _clientFactory.Create<IClientRolesResource>(accessToken);

        var allRoles = await rolesResource.GetAllAsync(realmId, cancellationToken).ConfigureAwait(false);

        // Preserve the server order for case-insensitive first-match lookup, including external case variants.
        // Fetch lazily when the first user with client roles is encountered.
        List<ClientRepresentation>? allClients = null;

        foreach (var user in users)
        {
            var representation = ConfigurationProjections.CreateUser(user);
            var matches = await usersResource.FindAsync(realmId, representation.Username!, cancellationToken).ConfigureAwait(false);
            var existing = matches.SingleOrDefault(u => u.Username == representation.Username);
            string? userId = existing?.Id;

            if (existing is null)
            {
                var response = await usersResource.CreateAsync(realmId, representation, cancellationToken).ConfigureAwait(false);
                userId = ResourceLocation.ExtractInternalIdFromLocationHeader(response, $"user '{representation.Username ?? "<unknown>"}'");
            }
            else
            {
                userId = existing.Id ?? throw new InvalidOperationException($"The user '{representation.Username}' does not have a Keycloak internal id.");
                var mergedRepresentation = ConfigurationProjections.UpdateUser(existing, representation);
                await usersResource.UpdateAsync(realmId, userId, mergedRepresentation, cancellationToken).ConfigureAwait(false);
            }

            foreach (var groupName in user.Groups)
            {
                var groupId = resolved.Groups.TryGetValue(groupName, out var knownGroupId)
                    ? knownGroupId
                    : await GroupLookup.FindGroupIdAsync(_clientFactory, realmId, groupName, accessToken, cancellationToken).ConfigureAwait(false);
                await usersResource.JoinGroupAsync(realmId, userId!, groupId, cancellationToken).ConfigureAwait(false);
            }

            if (user.Roles.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new InvalidOperationException($"The user '{representation.Username}' does not have a Keycloak internal id.");
                }

                await AssignRolesToUserAsync(realmId, userId, user.Roles, allRoles, usersResource, cancellationToken).ConfigureAwait(false);
            }

            if (user.ClientRoles.Count > 0)
            {
                if (string.IsNullOrWhiteSpace(userId))
                {
                    throw new InvalidOperationException($"The user '{representation.Username}' does not have a Keycloak internal id.");
                }

                allClients ??= await clientsResource.GetAllAsync(realmId, cancellationToken).ConfigureAwait(false);
                await AssignClientRolesToUserAsync(realmId, userId, user.ClientRoles, allClients, usersResource, clientRolesResource, cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static async Task AssignRolesToUserAsync(
        string realmId,
        string userId,
        IReadOnlyList<string> roleNames,
        IReadOnlyList<RoleRepresentation> allRoles,
        IUsersResource usersResource,
        CancellationToken cancellationToken)
    {
        var existingRoleMappings = await usersResource.GetRealmRoleMappingsAsync(realmId, userId, cancellationToken).ConfigureAwait(false);
        var existingRoleNames = existingRoleMappings
            .Select(role => role.Name)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        var rolesToAssign = allRoles
            .Where(r => roleNames.Contains(r.Name, StringComparer.OrdinalIgnoreCase)
                && !existingRoleNames.Contains(r.Name))
            .ToList();

        if (rolesToAssign.Count > 0)
        {
            await usersResource.AddRealmRoleMappingsAsync(realmId, userId, rolesToAssign, cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task AssignClientRolesToUserAsync(
        string realmId,
        string userId,
        IReadOnlyList<(string ClientId, string RoleName)> clientRoles,
        IReadOnlyList<ClientRepresentation> allClients,
        IUsersResource usersResource,
        IClientRolesResource clientRolesResource,
        CancellationToken cancellationToken)
    {
        // Group assignments by client to minimise API round-trips.
        var byClientId = clientRoles.GroupBy(r => r.ClientId, StringComparer.OrdinalIgnoreCase);

        foreach (var group in byClientId)
        {
            var clientId = group.Key;
            var client = allClients.FirstOrDefault(c => string.Equals(c.ClientId, clientId, StringComparison.OrdinalIgnoreCase))
                ?? throw new InvalidOperationException($"Client '{clientId}' not found in realm '{realmId}'. Ensure the client is provisioned before assigning client roles.");

            var clientInternalId = client.Id
                ?? throw new InvalidOperationException($"Client '{clientId}' does not have a Keycloak internal id.");

            var desiredRoleNames = group.Select(r => r.RoleName).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var availableClientRoles = await clientRolesResource.GetAllAsync(realmId, clientInternalId, cancellationToken).ConfigureAwait(false);
            var existingMappings = await usersResource.GetClientRoleMappingsAsync(realmId, userId, clientInternalId, cancellationToken).ConfigureAwait(false);
            var existingRoleNames = existingMappings.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

            var rolesToAssign = availableClientRoles
                .Where(r => r.Name is not null && desiredRoleNames.Contains(r.Name) && !existingRoleNames.Contains(r.Name))
                .ToList();

            if (rolesToAssign.Count > 0)
            {
                await usersResource.AddClientRoleMappingsAsync(realmId, userId, clientInternalId, rolesToAssign, cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
