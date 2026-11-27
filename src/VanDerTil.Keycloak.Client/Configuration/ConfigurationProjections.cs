using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Configuration;

internal static class ConfigurationProjections
{
    internal static RealmRepresentation CreateRealm(RealmConfiguration configuration) => new()
    {
        Id = configuration.RealmId,
        Realm = configuration.RealmId,
        DisplayName = configuration.DisplayName,
        Enabled = configuration.Enabled ?? false,
        SsoSessionIdleTimeout = configuration.SsoSessionIdleTimeout,
        SsoSessionMaxLifespan = configuration.SsoSessionMaxLifespan,
        RevokeRefreshToken = configuration.RevokeRefreshToken,
        RefreshTokenMaxReuse = configuration.RefreshTokenMaxReuse,
    };

    internal static ClientRepresentation CreateClient(ClientConfiguration configuration)
    {
        Dictionary<string, string>? attributes = configuration.Attributes.Count > 0 ? new(configuration.Attributes) : null;
        // Keycloak stores post-logout redirect URIs in one attribute, separated by "##".
        if (configuration.PostLogoutRedirectUris.Count > 0)
        {
            attributes ??= [];
            attributes["post.logout.redirect.uris"] = string.Join("##", configuration.PostLogoutRedirectUris);
        }
        return new ClientRepresentation
        {
            ClientId = configuration.ClientId,
            Name = configuration.Name,
            Secret = configuration.Secret,
            PublicClient = configuration.PublicClient ?? false,
            StandardFlowEnabled = configuration.StandardFlowEnabled ?? false,
            Enabled = configuration.Enabled ?? true,
            FullScopeAllowed = configuration.FullScopeAllowed ?? false,
            RedirectUris = configuration.RedirectUris.Count > 0 ? [.. configuration.RedirectUris] : null,
            Attributes = attributes,
        };
    }

    internal static ProtocolMapperRepresentation CreateMapper(ProtocolMapperConfiguration configuration) => new()
    {
        Name = configuration.Name,
        Protocol = "openid-connect",
        ProtocolMapper = configuration.ProtocolMapper,
        ConsentRequired = false,
        Config = configuration.Config.Count > 0 ? new(configuration.Config) : null,
    };

    internal static UserRepresentation CreateUser(UserConfiguration configuration)
    {
        var user = new UserRepresentation
        {
            Username = configuration.Username,
            FirstName = configuration.FirstName,
            LastName = configuration.LastName,
            Email = configuration.Email,
            Enabled = true,
        };
        if (configuration.TemporaryPassword is not null)
        {
            user.Credentials =
            [
                new CredentialRepresentation { Type = "password", Value = configuration.TemporaryPassword, Temporary = true },
            ];
        }
        if (configuration.Password is not null)
        {
            user.Credentials ??= [];
            user.Credentials.Add(new CredentialRepresentation
            {
                Type = "password",
                Value = configuration.Password,
                Temporary = false,
            });
        }
        return user;
    }

    internal static RealmRepresentation UpdateRealm(RealmRepresentation existing, RealmRepresentation desired, RealmConfiguration realm)
    {
        return new RealmRepresentation
        {
            AdditionalProperties = existing.AdditionalProperties,
            Id = existing.Id ?? desired.Id,
            Realm = existing.Realm ?? desired.Realm,
            SsoSessionIdleTimeout = desired.SsoSessionIdleTimeout ?? existing.SsoSessionIdleTimeout,
            SsoSessionMaxLifespan = desired.SsoSessionMaxLifespan ?? existing.SsoSessionMaxLifespan,
            RevokeRefreshToken = desired.RevokeRefreshToken ?? existing.RevokeRefreshToken,
            RefreshTokenMaxReuse = desired.RefreshTokenMaxReuse ?? existing.RefreshTokenMaxReuse,
            DisplayName = desired.DisplayName ?? existing.DisplayName,
            Enabled = realm.Enabled ?? existing.Enabled,
        };
    }

    internal static ClientRepresentation UpdateClient(ClientRepresentation existing, ClientRepresentation desired, ClientConfiguration client)
    {
        // Merge attributes: start from existing, then overlay desired values so that desired takes precedence.
        Dictionary<string, string>? mergedAttributes = null;

        if (existing.Attributes is { Count: > 0 } || desired.Attributes is { Count: > 0 })
        {
            mergedAttributes = new Dictionary<string, string>(existing.Attributes ?? []);

            if (desired.Attributes is not null)
            {
                foreach (var (key, value) in desired.Attributes)
                {
                    mergedAttributes[key] = value;
                }
            }
        }

        return new ClientRepresentation
        {
            AdditionalProperties = existing.AdditionalProperties,
            Id = existing.Id ?? desired.Id,
            ClientId = existing.ClientId ?? desired.ClientId,
            Name = desired.Name ?? existing.Name,
            // Keycloak does not return client secrets in GET responses, so existing.Secret is always null.
            // Only include Secret in the update payload when explicitly provided.
            Secret = string.IsNullOrWhiteSpace(desired.Secret) ? null : desired.Secret,
            FullScopeAllowed = client.FullScopeAllowed ?? existing.FullScopeAllowed,
            PublicClient = client.PublicClient ?? existing.PublicClient,
            StandardFlowEnabled = client.StandardFlowEnabled ?? existing.StandardFlowEnabled,
            RedirectUris = desired.RedirectUris is { Count: > 0 } ? desired.RedirectUris : existing.RedirectUris,
            Attributes = mergedAttributes,
            Enabled = client.Enabled ?? existing.Enabled,
        };
    }

    internal static ProtocolMapperRepresentation UpdateMapper(ProtocolMapperRepresentation existing, ProtocolMapperRepresentation desired)
    {
        return new ProtocolMapperRepresentation
        {
            Id = existing.Id,
            Name = desired.Name ?? existing.Name,
            Protocol = desired.Protocol ?? existing.Protocol,
            ProtocolMapper = desired.ProtocolMapper ?? existing.ProtocolMapper,
            ConsentRequired = desired.ConsentRequired,
            Config = desired.Config is { Count: > 0 } ? desired.Config : existing.Config,
        };
    }

    internal static UserRepresentation UpdateUser(UserRepresentation existing, UserRepresentation desired)
    {
        return new UserRepresentation
        {
            AdditionalProperties = existing.AdditionalProperties,
            Id = existing.Id ?? desired.Id,
            Username = existing.Username ?? desired.Username,
            FirstName = desired.FirstName ?? existing.FirstName,
            LastName = desired.LastName ?? existing.LastName,
            Email = desired.Email ?? existing.Email,
            Enabled = desired.Enabled,
            Credentials = desired.Credentials is { Count: > 0 } ? desired.Credentials : existing.Credentials,
        };
    }
}
