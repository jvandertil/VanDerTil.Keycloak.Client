using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IRealmsResource
{
    [Get("/admin/realms/{realm}")]
    Task<RealmRepresentation> GetAsync(string realm, CancellationToken cancellationToken = default);

    [Post("/admin/realms")]
    Task CreateAsync([Body] RealmRepresentation realm, CancellationToken cancellationToken = default);

    [Get("/admin/realms")]
    Task<List<RealmRepresentation>> GetAllAsync(CancellationToken cancellationToken = default);
}

internal interface IRealmResource
{
    [Get("/admin/realms/{realm}")]
    Task<RealmRepresentation> GetAsync(string realm, CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}")]
    Task UpdateAsync(string realm, [Body] RealmRepresentation body, CancellationToken cancellationToken = default);
}
