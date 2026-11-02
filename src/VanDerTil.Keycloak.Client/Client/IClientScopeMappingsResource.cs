using Refit;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IClientScopeMappingsResource
{
    [Get("/admin/realms/{realm}/clients/{clientId}/scope-mappings/clients/{roleClientId}")]
    Task<List<RoleRepresentation>> GetAsync(string realm, string clientId, string roleClientId, CancellationToken cancellationToken);

    [Post("/admin/realms/{realm}/clients/{clientId}/scope-mappings/clients/{roleClientId}")]
    Task AddAsync(string realm, string clientId, string roleClientId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken);

    [Delete("/admin/realms/{realm}/clients/{clientId}/scope-mappings/clients/{roleClientId}")]
    Task RemoveAsync(string realm, string clientId, string roleClientId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken);
}
