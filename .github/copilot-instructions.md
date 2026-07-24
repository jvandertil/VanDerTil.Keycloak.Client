# Copilot instructions for VanDerTil.Keycloak.Client

## Project purpose
This repository is a .NET 10 class library for provisioning Keycloak realms, clients, users, roles, and protocol mappers through the Keycloak admin API.

## Build, test, and validation
- Build from the repository root:
  - `dotnet build VanDerTil.Keycloak.Client.slnx --nologo`
- Test command:
  - `dotnet test VanDerTil.Keycloak.Client.slnx --nologo`
  - This repository currently has no test projects, so `dotnet test` reports that no tests were found.
- If a test project is added later, run one test by name with:
  - `dotnet test VanDerTil.Keycloak.Client.slnx --nologo --filter FullyQualifiedName~<TestName>`
- There is no dedicated lint script or formatter workflow configured in the repo. Keep changes compatible with the repository's `.editorconfig` and make sure `dotnet build` succeeds.

## High-level architecture
- The solution is intentionally small: one project under `src/VanDerTil.Keycloak.Client`.
- The public configuration surface is builder-based in `src/VanDerTil.Keycloak.Client/Configuration/`:
  - `KeycloakConfigurationBuilder` is the top-level entry point.
  - `RealmBuilder` owns clients, users, and roles for a single realm.
  - `ClientBuilder`, `UserBuilder`, and `ProtocolMapperBuilder` build the Keycloak objects that will be created or updated.
- The runtime orchestration layer is `src/VanDerTil.Keycloak.Client/Configuration/KeycloakConfigurator.cs`.
  - It reads the fluent configuration, compares the desired state against Keycloak, and performs create/update operations through the Refit-based resource interfaces.
- The Keycloak API boundary lives in `src/VanDerTil.Keycloak.Client/Client/`:
  - `RefitClientFactory` creates typed Refit clients with a bearer token.
  - Each `I*Resource` interface maps to one Keycloak admin REST resource (realms, clients, users, roles, protocol mappers).
- The request/response payload shape is defined in `src/VanDerTil.Keycloak.Client/Client/Representations/`.
  - These are plain DTOs that mirror Keycloak's admin API JSON shape.
  - When you add a setting, update both the relevant builder and the matching representation type.

## Repository conventions
- Keep the configuration API fluent and builder-oriented. New features should usually be added by extending the builder hierarchy rather than introducing parallel configuration paths.
- Follow the existing pattern where builders collect desired state and expose an internal `Build()` method that returns a representation object.
- When changing Keycloak provisioning behavior, wire it through `KeycloakConfigurator` so the new property affects both create and update flows.
- Prefer guard clauses at API boundaries:
  - `ArgumentNullException.ThrowIfNull(...)`
  - `ArgumentException.ThrowIfNullOrWhiteSpace(...)`
- The codebase uses file-scoped namespaces and follows the formatting/naming rules from `.editorconfig`.
- This project targets `net10.0`; keep changes compatible with the current SDK and package graph.

## Implementation notes for common changes
- Adding a new realm/client/user property usually means:
  1. Add a builder method/property in the relevant builder class.
  2. Apply the value in the builder's `Build()` result.
  3. Update the corresponding representation class if the payload needs a new JSON field.
  4. Update `KeycloakConfigurator` if the property needs special create/update handling.
- For protocol mappers, use `ProtocolMapperBuilder` and its factory methods (`UserClientRolesToClaim`) instead of hand-rolling raw mapper dictionaries.
- For client roles and user role assignment, preserve the existing idempotent behavior: compare against existing Keycloak state before creating missing roles or mappings.
