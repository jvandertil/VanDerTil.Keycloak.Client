using Refit;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IGroupsResource
{
    [Get("/admin/realms/{realm}/groups?first=0&max=100&exact=true")]
    Task<List<GroupRepresentation>> FindAsync(string realm, string search, CancellationToken cancellationToken);

    [Get("/admin/realms/{realm}/group-by-path/{path}")]
    Task<ApiResponse<GroupRepresentation>> GetByPathAsync(string realm, string path, CancellationToken cancellationToken);

    [Post("/admin/realms/{realm}/groups/{parentId}/children")]
    Task<IApiResponse> CreateChildAsync(string realm, string parentId, [Body] GroupRepresentation group, CancellationToken cancellationToken);

    [Post("/admin/realms/{realm}/groups")]
    Task<IApiResponse> CreateAsync(string realm, [Body] GroupRepresentation group, CancellationToken cancellationToken);

    [Get("/admin/realms/{realm}/groups/{groupId}/role-mappings/clients/{clientId}")]
    Task<List<RoleRepresentation>> GetRolesAsync(string realm, string groupId, string clientId, CancellationToken cancellationToken);

    [Post("/admin/realms/{realm}/groups/{groupId}/role-mappings/clients/{clientId}")]
    Task AddRolesAsync(string realm, string groupId, string clientId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken);

    [Delete("/admin/realms/{realm}/groups/{groupId}/role-mappings/clients/{clientId}")]
    Task RemoveRolesAsync(string realm, string groupId, string clientId, [Body] List<RoleRepresentation> roles, CancellationToken cancellationToken);
}
