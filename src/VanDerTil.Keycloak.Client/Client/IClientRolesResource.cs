using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IClientRolesResource
{
    [Get("/admin/realms/{realm}/clients/{clientInternalId}/roles")]
    Task<List<RoleRepresentation>> GetAllAsync(string realm, string clientInternalId, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/clients/{clientInternalId}/roles")]
    Task CreateAsync(string realm, string clientInternalId, [Body] RoleRepresentation role, CancellationToken cancellationToken = default);
}
