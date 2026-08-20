# VanDerTil.Keycloak.Client

A fluent builder and provisioning engine for the Keycloak admin API. Configure realms, clients, users, roles, and protocol mappers with a desired state configuration approach. Supports idempotent provisioning for reliable infrastructure-as-code workflows.

## Features

- **Fluent API** - Intuitive builder pattern for defining Keycloak configuration
- **Idempotent Provisioning** - Safe to run multiple times; updates only what changed
- **Multiple Authentication Modes** - Direct grant, client credentials, or custom token providers
- **Protocol Mapper Support** - Built-in helpers for common OIDC/SAML mappers
- **Dependency Injection Ready** - First-class support for ASP.NET Core DI
- **Partial Updates** - Only updates configured properties; preserves manual changes

## Installation

```bash
dotnet add package VanDerTil.Keycloak.Client
```

## Quick Start

### 1. Configure Authentication

The library supports three authentication modes:

#### Option A: Client Credentials (Recommended for Production)

First, create a service account client in Keycloak:
1. Create a new client in the master realm (e.g., `keycloak-provisioner`)
2. Enable "Client authentication"
3. Enable "Service accounts roles"
4. In the "Service account roles" tab, assign admin roles:
   - `realm-management` → `realm-admin` (for realm operations)
   - `master-realm` → `admin` (for master realm access)

**appsettings.json:**
```json
{
  "Keycloak": {
    "Connection": {
      "BaseUrl": "https://keycloak.example.com",
      "AdminRealm": "master",
      "AuthenticationMode": "ClientCredentials",
      "ClientId": "keycloak-provisioner",
      "ClientSecret": "your-client-secret"
    }
  }
}
```

**Startup.cs:**
```csharp
builder.Services.AddKeycloakClient(builder.Configuration);
```

#### Option B: Direct Grant (Development Only)

**⚠️ Warning:** Not recommended for production. Use only for local development.

**appsettings.json:**
```json
{
  "Keycloak": {
    "Connection": {
      "BaseUrl": "http://localhost:8080",
      "AdminRealm": "master",
      "AuthenticationMode": "DirectGrant",
      "AdminUsername": "admin",
      "AdminPassword": "admin"
    }
  }
}
```

#### Option C: External Token Provider

For advanced scenarios (managed identities, custom flows):

```csharp
builder.Services.AddKeycloakClient(builder.Configuration, options =>
{
    options.AuthenticationMode = AuthenticationMode.ExternalTokenProvider;
    options.TokenProvider = async (ct) =>
    {
        // Your custom token acquisition logic
        return await GetTokenFromAzureKeyVaultOrManagedIdentity(ct);
    };
});
```

### 2. Define and Provision Configuration

```csharp
using VanDerTil.Keycloak.Client;
using VanDerTil.Keycloak.Client.Configuration;

var app = builder.Build();

// Obtain access token
var tokenProvider = app.Services.GetRequiredService<OpenIdConnectAccessTokenProvider>();
var accessToken = await tokenProvider.GetAuthorizationTokenAsync(CancellationToken.None);

// Define and apply configuration
var configurator = app.Services.GetRequiredService<IKeycloakConfigurator>();
await configurator.ConfigureAsync(
    config =>
    {
        config.AddRealm("my-app", realm =>
        {
            realm.WithDisplayName("My Application");
            realm.Enabled();

            // Add realm-level role
            realm.AddRole("app-user");

            // Add a client
            realm.AddClient("my-app-web", client =>
            {
                client.Confidential();
                client.WithSecret("client-secret");
                client.WithStandardFlow();
                client.WithRedirectUri("https://myapp.example.com/signin-oidc");
                client.WithPostLogoutRedirectUri("https://myapp.example.com/signout-callback-oidc");

                // Add client-level roles
                client.AddRole("admin");
                client.AddRole("user");

                // Add protocol mapper
                client.AddProtocolMapper(
                    ProtocolMapperBuilder.UserClientRolesToClaim("client-roles", "roles"));
            });

            // Add a user
            realm.AddUser("john.doe", user =>
            {
                user.WithEmail("john.doe@example.com");
                user.WithFirstName("John");
                user.WithLastName("Doe");
                user.WithTemporaryPassword("temporary-password");

                // Assign realm role
                user.InRole("app-user");

                // Assign client role
                user.InClientRole("my-app-web", "admin");
            });
        });
    },
    accessToken,
    CancellationToken.None);
```

## Complete Examples

### Example 1: Basic Realm Setup

```csharp
config.AddRealm("production", realm =>
{
    realm.WithDisplayName("Production Environment");
    realm.Enabled();
});
```

### Example 2: Public Client (SPA)

```csharp
realm.AddClient("my-spa", client =>
{
    client.Public(); // No client secret
    client.WithStandardFlow();
    client.WithRedirectUri("http://localhost:4200/*");
    client.WithPostLogoutRedirectUri("http://localhost:4200/");
});
```

### Example 3: Confidential Client with Custom Claims

```csharp
realm.AddClient("backend-api", client =>
{
    client.Confidential();
    client.WithSecret("api-secret");
    client.WithStandardFlow();

    // Add roles that will be mapped to tokens
    client.AddRole("read-data");
    client.AddRole("write-data");

    // Map client roles to a token claim
    client.AddProtocolMapper(
        ProtocolMapperBuilder.UserClientRolesToClaim(
            "api-roles", // Mapper name
            "api_roles"  // Claim name in token
        ));
});
```

### Example 4: User Provisioning with Multiple Roles

```csharp
realm.AddUser("jane.admin", user =>
{
    user.WithEmail("jane@example.com");
    user.WithFirstName("Jane");
    user.WithLastName("Admin");
    user.WithTemporaryPassword("temp123!");

    // Realm-level role
    user.InRole("app-user");

    // Multiple client roles
    user.InClientRole("backend-api", "read-data");
    user.InClientRole("backend-api", "write-data");
    user.InClientRole("my-app-web", "admin");
});
```

### Example 5: Running at Startup

```csharp
public class KeycloakProvisioningService : IHostedService
{
    private readonly IKeycloakConfigurator _configurator;
    private readonly OpenIdConnectAccessTokenProvider _tokenProvider;
    private readonly ILogger<KeycloakProvisioningService> _logger;

    public KeycloakProvisioningService(
        IKeycloakConfigurator configurator,
        OpenIdConnectAccessTokenProvider tokenProvider,
        ILogger<KeycloakProvisioningService> logger)
    {
        _configurator = configurator;
        _tokenProvider = tokenProvider;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Provisioning Keycloak configuration...");

        var accessToken = await _tokenProvider.GetAuthorizationTokenAsync(cancellationToken);

        await _configurator.ConfigureAsync(
            config =>
            {
                // Your configuration here
            },
            accessToken,
            cancellationToken);

        _logger.LogInformation("Keycloak configuration provisioning complete.");
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

// Register in Program.cs
builder.Services.AddHostedService<KeycloakProvisioningService>();
```

## Authentication Setup Details

### Setting Up Client Credentials Authentication

1. **Login to Keycloak Admin Console**
2. **Navigate to the Master Realm** (or your admin realm)
3. **Create a new Client:**
   - Client ID: `keycloak-provisioner`
   - Client Protocol: `openid-connect`
   - Access Type: `confidential`
   - Service Accounts Enabled: `ON`
   - Save
4. **Copy the Client Secret** from the Credentials tab
5. **Assign Service Account Roles:**
   - Go to the "Service Account Roles" tab
   - Assign appropriate realm management roles:
     - For full admin access: `realm-management` → `realm-admin`
     - For specific operations, assign granular roles like `manage-clients`, `manage-users`, etc.

### Security Best Practices

- **Never commit secrets to source control**
- **Use Azure Key Vault, AWS Secrets Manager, or similar for production secrets**
- **Prefer Client Credentials flow over Direct Grant for production**
- **Use principle of least privilege** - only assign roles the service account actually needs
- **Rotate secrets regularly**
- **Enable HTTPS** for production Keycloak instances

**Example with Azure Key Vault:**

```csharp
builder.Services.AddKeycloakClient(builder.Configuration, options =>
{
    options.ClientSecret = builder.Configuration["KeyVault:KeycloakClientSecret"];
});
```

## Behavior and Limitations

### Idempotency

The library is designed for safe repeated execution:
- **Create operations** check if the resource exists first
- **Update operations** merge your desired state with existing Keycloak state
- **Role assignments** only add missing roles (never remove)

### Partial Updates (No Drift Detection)

**Important:** The library performs *partial updates* — it only modifies properties you explicitly configure.

**Example:**
```csharp
realm.AddClient("my-client", client =>
{
    client.WithRedirectUri("https://app.example.com/callback");
});
```

If someone manually adds a second redirect URI in Keycloak (`https://app.example.com/callback2`), the provisioning **will not remove it**. This is by design for flexibility.

**Implications:**
- ✅ Safe to provision incrementally
- ✅ Manual Keycloak changes are preserved
- ⚠️ No automatic cleanup of unmanaged properties
- ⚠️ No drift detection or reporting (future feature)

### When Provisioning Runs

The library does **not** automatically run provisioning. You control when it executes:

- **Startup** - Use `IHostedService` (recommended for apps)
- **On-demand** - Call `ConfigureAsync` from a controller or CLI command
- **Scheduled** - Use a background worker or cron job

## Architecture

### Components

- **`KeycloakConfigurationBuilder`** - Top-level entry point; collects realms
- **`RealmBuilder`** - Defines a realm and its clients, users, roles
- **`ClientBuilder`** - Configures OIDC/SAML clients
- **`UserBuilder`** - Provisions users with credentials and role assignments
- **`ProtocolMapperBuilder`** - Creates token claim mappers
- **`KeycloakConfigurator`** - Orchestrates the provisioning workflow
- **`OpenIdConnectAccessTokenProvider`** - Obtains admin API access tokens
- **`RefitClientFactory`** - Creates typed HTTP clients for Keycloak REST API

### Provisioning Flow

1. Obtain admin access token (via configured authentication mode)
2. Build desired state from fluent configuration
3. Fetch existing Keycloak state
4. Calculate diff and apply changes:
   - Create missing realms/clients/users
   - Update existing resources (merge semantics)
   - Assign roles that aren't already assigned

## API Reference

### RealmBuilder

| Method | Description |
|--------|-------------|
| `WithDisplayName(string)` | Sets the realm's display name |
| `Enabled()` | Enables the realm |
| `AddClient(string, Action<ClientBuilder>)` | Adds a client to the realm |
| `AddUser(string, Action<UserBuilder>)` | Adds a user to the realm |
| `AddRole(string)` | Adds a realm-level role |

### ClientBuilder

| Method | Description |
|--------|-------------|
| `WithName(string)` | Sets the client's display name |
| `Public()` | Marks the client as public (no secret) |
| `Confidential()` | Marks the client as confidential (has secret) |
| `WithSecret(string)` | Sets the client secret |
| `WithStandardFlow()` | Enables standard OpenID Connect flow |
| `WithRedirectUri(string)` | Adds a redirect URI |
| `WithPostLogoutRedirectUri(string)` | Adds a post-logout redirect URI |
| `Enabled()` | Enables the client |
| `AddRole(string)` | Adds a client-level role |
| `AddProtocolMapper(ProtocolMapperBuilder)` | Adds a protocol mapper |

### UserBuilder

| Method | Description |
|--------|-------------|
| `WithEmail(string)` | Sets the user's email |
| `WithFirstName(string)` | Sets the user's first name |
| `WithLastName(string)` | Sets the user's last name |
| `WithPassword(string)` | Sets a non-temporary password |
| `WithTemporaryPassword(string)` | Sets a temporary password (user must change on first login) |
| `InRole(string)` | Assigns a realm-level role |
| `InClientRole(string, string)` | Assigns a client-level role |

### ProtocolMapperBuilder

| Factory Method | Description |
|----------------|-------------|
| `UserClientRolesToClaim(string, string)` | Maps user's client roles to a token claim |

## Contributing

Contributions are welcome! Please:

1. Fork the repository
2. Create a feature branch
3. Add tests for new functionality
4. Ensure all tests pass
5. Submit a pull request

### Running Tests

Tests use Testcontainers to spin up a Keycloak instance. Docker is required.

```bash
dotnet test
```

### Building

```bash
dotnet build VanDerTil.Keycloak.Client.slnx
```

## License

MIT License - see [LICENSE](LICENSE) for details

## Support

- 🐛 **Issues:** [GitHub Issues](https://github.com/jvandertil/VanDerTil.Keycloak.Client/issues)
- 📖 **Documentation:** [GitHub Repository](https://github.com/jvandertil/VanDerTil.Keycloak.Client)

## Changelog

### v0.9.0-preview (Pre-release)
- Initial preview release
- Fluent configuration API
- Idempotent provisioning
- Multiple authentication modes (direct grant, client credentials, external provider)
- Protocol mapper support
- Role assignment

---

**Positioning:** Think of this as "Terraform-like provisioning for Keycloak using .NET" — declarative, idempotent infrastructure-as-code.
