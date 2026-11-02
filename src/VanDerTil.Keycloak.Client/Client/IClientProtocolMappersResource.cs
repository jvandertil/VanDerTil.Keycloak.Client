using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IClientProtocolMappersResource
{
    [Get("/admin/realms/{realm}/clients/{clientInternalId}/protocol-mappers/models")]
    Task<List<ProtocolMapperRepresentation>> GetAllAsync(string realm, string clientInternalId, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/clients/{clientInternalId}/protocol-mappers/models")]
    Task CreateAsync(string realm, string clientInternalId, [Body] ProtocolMapperRepresentation mapper, CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/clients/{clientInternalId}/protocol-mappers/models/{mapperId}")]
    Task UpdateAsync(string realm, string clientInternalId, string mapperId, [Body] ProtocolMapperRepresentation mapper, CancellationToken cancellationToken = default);
}
