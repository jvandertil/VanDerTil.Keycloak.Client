using System.Linq;

using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed partial class KeycloakConfigurator : IKeycloakConfigurator
{
    private readonly RefitClientFactory _clientFactory;
    private readonly OpenIdConnectAccessTokenProvider _accessTokenProvider;

    public KeycloakConfigurator(RefitClientFactory clientFactory, OpenIdConnectAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        _clientFactory = clientFactory;
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task ConfigureAsync(Action<KeycloakConfigurationBuilder> configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new KeycloakConfigurationBuilder();
        configure(builder);

        var configuration = builder.Build();
        foreach (var realm in configuration.Realms)
        {
            await ConfigureRealmAsync(realm, accessToken, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConfigureRealmAsync(RealmConfiguration realm, string accessToken, CancellationToken cancellationToken)
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

        await ConfigureRolesAsync(realmId, realm.Roles, accessToken, cancellationToken).ConfigureAwait(false);
        await ConfigureClientsAsync(realmId, realm.Clients, accessToken, cancellationToken).ConfigureAwait(false);
        await ConfigureClientScopesAsync(realmId, realm.Clients, accessToken, cancellationToken).ConfigureAwait(false);
        await ConfigureGroupsAsync(realmId, realm.Groups, accessToken, cancellationToken).ConfigureAwait(false);
        await ConfigureUsersAsync(realmId, realm.Users, accessToken, cancellationToken).ConfigureAwait(false);
    }

    private async Task ConfigureRolesAsync(string realmId, IReadOnlyList<string> roles, string accessToken, CancellationToken cancellationToken)
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

    private async Task ConfigureClientsAsync(string realmId, IReadOnlyList<ClientConfiguration> clients, string accessToken, CancellationToken cancellationToken)
    {
        if (clients.Count == 0)
        {
            return;
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
                internalId = ExtractInternalIdFromLocationHeader(response, $"client '{representation.ClientId}'");
            }
            else
            {
                internalId = existing.Id ?? throw new InvalidOperationException($"The client '{representation.ClientId}' does not have a Keycloak internal id.");
                var mergedRepresentation = ConfigurationProjections.UpdateClient(existing, representation, client);
                await clientsResource.UpdateAsync(realmId, internalId, mergedRepresentation, cancellationToken).ConfigureAwait(false);
            }

            await ConfigureClientRolesAsync(realmId, internalId, client.Roles, accessToken, cancellationToken).ConfigureAwait(false);
            await ConfigureClientProtocolMappersAsync(realmId, internalId, client.ProtocolMappers, accessToken, cancellationToken).ConfigureAwait(false);
        }
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

    private async Task ConfigureUsersAsync(
        string realmId, IReadOnlyList<UserConfiguration> users, string accessToken, CancellationToken cancellationToken)
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

        // Fetched lazily — only when the first user with client roles is encountered.
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
                userId = ExtractUserIdFromLocationHeader(response, representation.Username ?? "<unknown>");
            }
            else
            {
                userId = existing.Id ?? throw new InvalidOperationException($"The user '{representation.Username}' does not have a Keycloak internal id.");
                var mergedRepresentation = ConfigurationProjections.UpdateUser(existing, representation);
                await usersResource.UpdateAsync(realmId, userId, mergedRepresentation, cancellationToken).ConfigureAwait(false);
            }

            foreach (var groupName in user.Groups)
            {
                var groupId = await FindGroupIdAsync(realmId, groupName, accessToken, cancellationToken).ConfigureAwait(false);
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

    private static string ExtractInternalIdFromLocationHeader(Refit.IApiResponse response, string resourceDescription)
    {
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Failed to create {resourceDescription} in Keycloak. Status code: {response.StatusCode}, Response: {response.Error}");
        }

        var location = response.Headers.Location
            ?? throw new InvalidOperationException($"Keycloak did not return a Location header after creating {resourceDescription}.");

        var internalId = location.Segments[^1].TrimEnd('/');

        if (string.IsNullOrWhiteSpace(internalId))
        {
            throw new InvalidOperationException($"Could not extract Keycloak internal id from Location header for {resourceDescription}.");
        }

        return internalId;
    }

    private static string ExtractUserIdFromLocationHeader(Refit.IApiResponse response, string username)
    {
        return ExtractInternalIdFromLocationHeader(response, $"user '{username}'");
    }

}
