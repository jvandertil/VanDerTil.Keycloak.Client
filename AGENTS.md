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

### Framework and assertions
- Use MSTest through the repository's `MSTest.Sdk` and Microsoft Testing Platform configuration in `global.json`.
- Use Shouldly for assertions. Prefer focused assertions such as `ShouldBe`, `ShouldContain`, and `ShouldHaveSingleItem`; use `Should.Throw<TException>` and `Should.ThrowAsync<TException>` for exception behavior instead of raw `Assert.*` where an equivalent exists.
- Do not add Moq or FluentAssertions. Prefer small handwritten stubs or spies at external system boundaries, and use NSubstitute sparingly only when it makes a boundary test clearer.

### Naming and structure
- Organize tests by feature or capability, rather than mirroring every implementation class. Keep the test folder structure aligned with the area under test, such as configuration behavior and Keycloak integration workflows.
- Name test classes for the capability or scenario. Use nested `[TestClass]` classes with noun-phrase names when several tests share a scenario context; a single-test class does not need an extra nesting level.
- Name test methods `Should_<ExpectedBehavior>`. Append `_When_<Condition>` only to distinguish sibling outcomes when the scenario context does not already make the condition clear.
- Structure each test as Arrange–Act–Assert and label the sections with `// Arrange`, `// Act`, and `// Assert`. Each test should tell one behavior story; several assertions are appropriate when they describe outcomes of that same behavior.
- Extract repeated setup into private helpers or shared scenario setup. Use test data builders with realistic defaults when constructing non-trivial objects across tests, and override only the values relevant to each scenario.

### Test value and boundaries
- Prefer tests that protect meaningful behavior at a maintenance cost justified by the regression risk. There is no required coverage percentage.
- Assert observable results, built representations, and persisted Keycloak state. Do not test private methods, expose internals for tests, duplicate Keycloak's own behavior, or bind tests to implementation details and collaborator call sequences.
- Keep collaborators within this library real for unit tests. Use test doubles at external boundaries; use the real Keycloak service for provisioning integration tests.
- Cover important success, failure, idempotency, reconciliation, and preservation behavior without making a large number of end-to-end tests. Keep framework and HTTP plumbing thin and test complex decisions through public behavior.

### Keycloak integration tests
- Mark tests that require external infrastructure with `[TestCategory("Integration")]`.
- Use the real Keycloak container provided by Testcontainers. The full suite therefore requires Docker; do not replace Keycloak with an in-memory substitute for provisioning integration tests.
- Give each test a unique realm identifier and avoid depending on test order or shared realm state. Method-level parallel execution is enabled, so tests must remain isolated when they run concurrently.
- Match fixture cleanup to its lifetime: use `[TestCleanup]` for per-test resources, `[ClassCleanup]` for class-scoped resources, and `[AssemblyCleanup]` for assembly-scoped resources. Pair assembly-scoped resources with `[AssemblyInitialize]`.
- The current Keycloak container is class-scoped, so initialize it once for the integration test class and dispose it and its service provider during `[ClassCleanup]`. Move it to assembly scope only if multiple integration classes benefit from sharing startup, and ensure focused unit-test runs do not unnecessarily start Docker.

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
