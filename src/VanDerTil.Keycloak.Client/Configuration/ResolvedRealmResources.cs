using System.Collections.Immutable;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed record ResolvedRealmResources
{
    internal ImmutableDictionary<string, string> Clients { get; init; } = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
    internal ImmutableDictionary<string, string> Groups { get; init; } = ImmutableDictionary.Create<string, string>(StringComparer.Ordinal);
}
