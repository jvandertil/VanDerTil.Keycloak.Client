using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class ClientReconciler
{
    private readonly RefitClientFactory _clientFactory;

    internal ClientReconciler(RefitClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }

    internal async Task<ResolvedRealmResources> ReconcileAsync(string realmId, IReadOnlyList<ClientConfiguration> clients, string accessToken, ResolvedRealmResources resolved, CancellationToken cancellationToken)
    {
        if (clients.Count == 0)
        {
            return resolved;
        }

        var clientsResource = _clientFactory.Create<IClientsResource>(accessToken);
        var existingClients = await clientsResource.GetAllAsync(realmId, cancellationToken).ConfigureAwait(false);

        foreach (var client in clients)
        {
            var representation = ConfigurationProjections.CreateClient(client);
            var existing = existingClients.FirstOrDefault(c => c.ClientId == representation.ClientId);
            string internalId;

            if (existing is null)
            {
                var response = await clientsResource.CreateAsync(realmId, representation, cancellationToken).ConfigureAwait(false);
                internalId = ResourceLocation.ExtractInternalIdFromLocationHeader(response, $"client '{representation.ClientId}'");
            }
            else
            {
                internalId = existing.Id ?? throw new InvalidOperationException($"The client '{representation.ClientId}' does not have a Keycloak internal id.");
                var mergedRepresentation = ConfigurationProjections.UpdateClient(existing, representation, client);
                await clientsResource.UpdateAsync(realmId, internalId, mergedRepresentation, cancellationToken).ConfigureAwait(false);
            }

            resolved = resolved with { Clients = resolved.Clients.SetItem(client.ClientId, internalId) };

            await ConfigureClientRolesAsync(realmId, internalId, client.Roles, accessToken, cancellationToken).ConfigureAwait(false);
            await ConfigureClientProtocolMappersAsync(realmId, internalId, client.ProtocolMappers, accessToken, cancellationToken).ConfigureAwait(false);
        }
        return resolved;
    }

    private async Task ConfigureClientRolesAsync(
        string realmId,
        string clientInternalId,
        IReadOnlyList<string> roles,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (roles.Count == 0)
        {
            return;
        }

        var clientRolesResource = _clientFactory.Create<IClientRolesResource>(accessToken);
        var existingRoles = await clientRolesResource.GetAllAsync(realmId, clientInternalId, cancellationToken).ConfigureAwait(false);
        var existingRoleNames = existingRoles.Select(r => r.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roles)
        {
            if (!existingRoleNames.Contains(roleName))
            {
                await clientRolesResource.CreateAsync(realmId, clientInternalId, new RoleRepresentation { Name = roleName }, cancellationToken).ConfigureAwait(false);
                existingRoleNames.Add(roleName);
            }
        }
    }

    private async Task ConfigureClientProtocolMappersAsync(
        string realmId,
        string clientInternalId,
        IReadOnlyList<ProtocolMapperConfiguration> mappers,
        string accessToken,
        CancellationToken cancellationToken)
    {
        if (mappers.Count == 0)
        {
            return;
        }

        var mappersResource = _clientFactory.Create<IClientProtocolMappersResource>(accessToken);
        var existingMappers = await mappersResource.GetAllAsync(realmId, clientInternalId, cancellationToken).ConfigureAwait(false);

        foreach (var mapper in mappers)
        {
            var desired = ConfigurationProjections.CreateMapper(mapper);
            var existing = existingMappers.FirstOrDefault(m => string.Equals(m.Name, desired.Name, StringComparison.OrdinalIgnoreCase));

            if (existing is null)
            {
                await mappersResource.CreateAsync(realmId, clientInternalId, desired, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var mapperId = existing.Id ?? throw new InvalidOperationException($"Protocol mapper '{desired.Name}' does not have a Keycloak internal id.");
                await mappersResource.UpdateAsync(realmId, clientInternalId, mapperId, ConfigurationProjections.UpdateMapper(existing, desired), cancellationToken).ConfigureAwait(false);
            }
        }
    }
}
