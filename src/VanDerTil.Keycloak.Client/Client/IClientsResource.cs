using Refit;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

internal interface IClientsResource
{
    [Get("/admin/realms/{realm}/clients")]
    Task<List<ClientRepresentation>> GetAllAsync(string realm, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/clients")]
    Task<IApiResponse> CreateAsync(string realm, [Body] ClientRepresentation client, CancellationToken cancellationToken = default);

    [Put("/admin/realms/{realm}/clients/{clientId}")]
    Task UpdateAsync(string realm, string clientId, [Body] ClientRepresentation client, CancellationToken cancellationToken = default);

    [Get("/admin/realms/{realm}/clients/{clientId}/client-secret")]
    Task<ClientSecretRepresentation> GetClientSecretAsync(string realm, string clientId, CancellationToken cancellationToken = default);

    [Post("/admin/realms/{realm}/clients/{clientId}/client-secret")]
    Task<ClientSecretRepresentation> RegenerateClientSecretAsync(string realm, string clientId, CancellationToken cancellationToken = default);
}
