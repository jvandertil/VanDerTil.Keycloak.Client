namespace VanDerTil.Keycloak.Client.Configuration;

internal static class ResourceLocation
{

    internal static string ExtractInternalIdFromLocationHeader(Refit.IApiResponse response, string resourceDescription)
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

}
