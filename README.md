# VanDerTil.Keycloak.Client

This package provides a fluent builder model for describing the desired Keycloak state and a small provisioning engine that applies that state to a Keycloak admin API.

## Recommended consumption model

1. Register the package in the consuming application's service container.
2. Bind Keycloak connection settings from configuration.
3. Define the desired realms, clients, users, and roles with `KeycloakConfigurationBuilder`.
4. Run the provisioning flow from startup or an initialization background service.

Example:

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VanDerTil.Keycloak.Client;
using VanDerTil.Keycloak.Client.Configuration;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddKeycloakClient(builder.Configuration);

var app = builder.Build();

app.Services.GetRequiredService<IKeycloakConfigurator>()
    .ConfigureAsync(
        configurationBuilder =>
        {
            configurationBuilder.AddRealm("sample-app", realm =>
            {
                realm.WithDisplayName("Sample App");
                realm.Enabled();

                realm.AddClient("sample-app-web", client =>
                {
                    client.Confidential();
                    client.WithStandardFlow();
                    client.AddProtocolMapper(ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"));
                });
            });
        },
        accessToken,
        CancellationToken.None);
```

Keep the application-specific realm/client/user structure in the consuming application. The package should remain generic and builder-driven so it can be reused across different applications. If you need convenience methods that overlay configuration sections onto builders, implement them as extension methods in the consuming application rather than baking them into this package.
