using System.Linq;
using VanDerTil.Keycloak.Client.Client;
using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal static class GroupLookup
{
    internal static async Task<string> FindGroupIdAsync(RefitClientFactory clientFactory, string realm, string name, string token, CancellationToken ct)
    {
        return (await FindGroupAsync(clientFactory, realm, name, token, ct).ConfigureAwait(false))?.Id
            ?? throw new InvalidOperationException($"Top-level group '{name}' not found in realm '{realm}'.");
    }

    internal static async Task<GroupRepresentation?> FindGroupAsync(RefitClientFactory clientFactory, string realm, string name, string token, CancellationToken ct)
    {
        using var response = await clientFactory.Create<IGroupsResource>(token).GetByPathAsync(realm, name, ct).ConfigureAwait(false);
        if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
        await response.EnsureSuccessStatusCodeAsync().ConfigureAwait(false);
        return response.Content ?? throw new InvalidOperationException($"Keycloak returned no group for '{name}'.");
    }
}
