using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IUsersResource
{
    [Get("/admin/realms/{realm}/users")]
    Task<List<UserRepresentation>> GetAllAsync(string realm, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/users")]
    Task<IApiResponse> CreateAsync(string realm, [Body] UserRepresentation user, CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/users/{userId}")]
    Task UpdateAsync(string realm, string userId, [Body] UserRepresentation user, CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users/{userId}/role-mappings/realm")]
    Task<List<RoleRepresentation>> GetRealmRoleMappingsAsync(string realm, string userId, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/users/{userId}/role-mappings/realm")]
    Task AddRealmRoleMappingsAsync(string realm, string userId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/users/{userId}/role-mappings/clients/{clientInternalId}")]
    Task<List<RoleRepresentation>> GetClientRoleMappingsAsync(string realm, string userId, string clientInternalId, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/users/{userId}/role-mappings/clients/{clientInternalId}")]
    Task AddClientRoleMappingsAsync(string realm, string userId, string clientInternalId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken = default);
}
