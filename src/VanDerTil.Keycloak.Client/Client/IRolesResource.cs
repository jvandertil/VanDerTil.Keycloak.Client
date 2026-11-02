using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IRolesResource
{
    [Get("/admin/realms/{realm}/roles")]
    Task<List<RoleRepresentation>> GetAllAsync(string realm, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/roles")]
    Task CreateAsync(string realm, [Body] RoleRepresentation role, CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/roles/{roleName}")]
    Task<RoleRepresentation> GetByNameAsync(string realm, string roleName, CancellationToken cancellationToken = default);
}
