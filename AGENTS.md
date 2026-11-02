# Agent instructions for VanDerTil.Keycloak.Client

## Project purpose
This repository is a .NET 10 class library for provisioning Keycloak realms, clients, users, roles, and protocol mappers through the Keycloak admin API.

## Build, test, and validation
- Build from the repository root:
  - `dotnet build VanDerTil.Keycloak.Client.slnx --configuration Release --nologo`
- Run the full test suite:
  - `dotnet test VanDerTil.Keycloak.Client.slnx --configuration Release --nologo`
- Run a focused test by fully qualified name:
  - `dotnet test VanDerTil.Keycloak.Client.slnx --configuration Release --nologo --filter "FullyQualifiedName~<TestName>"`
- The full test suite includes Keycloak integration tests using Testcontainers and requires an available Docker daemon. A Keycloak image may need to be downloaded the first time.
- When changing package dependencies, update the package lock files and verify them with `dotnet restore VanDerTil.Keycloak.Client.slnx --locked-mode`.
- Keep changes compatible with the repository's `.editorconfig` and make sure the Release build succeeds.

## Testing instructions

- Use the repository's MSTest and Shouldly setup. Prefer simple handwritten stubs at external boundaries when unit tests need doubles; exercise library components together rather than mocking them.
- Organize tests by behavior or capability. Give tests descriptive names, keep setup proportionate, and extract reusable scenarios when they improve readability.
- Test observable results, generated representations, and persisted Keycloak state instead of private methods or collaborator call sequences. Prioritize meaningful regression risks: failures, repeated provisioning, reconciliation, and preserving unmanaged state.
- Mark tests requiring Keycloak with `[TestCategory("Integration")]` and use the real Testcontainers service. Each test must use its own realm so method-level parallel execution remains safe.
- The Keycloak fixture is lazy and assembly-scoped: it starts on first integration-test use and is disposed in `[AssemblyCleanup]`. Keep unit-only test runs independent of Docker.

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
