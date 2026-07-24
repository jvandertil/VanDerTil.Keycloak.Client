namespace VanDerTil.Keycloak.Client;

public sealed class KeycloakConnectionOptions
{
    public const string SectionName = "Keycloak:Connection";

    /// <summary>The base URL of the Keycloak instance.</summary>
    public string BaseUrl { get; set; } = "http://localhost:43000";

    /// <summary>Admin credentials used to obtain a bootstrap access token.</summary>
    public string AdminUsername { get; set; } = string.Empty;

    /// <summary>Admin credentials used to obtain a bootstrap access token.</summary>
    public string AdminPassword { get; set; } = string.Empty;

    public string AdminRealm { get; set; } = string.Empty;

    /// <summary>When true, a development seed user is created during configuration.</summary>
    public bool SeedTestUser { get; set; }

    /// <summary>Optional Azure Key Vault source for secrets.</summary>
    public KeyVaultOptions KeyVault { get; set; } = new();

    public sealed class KeyVaultOptions
    {
        public string? VaultUri { get; set; }

        public string? ManagedIdentityClientId { get; set; }
    }
}
