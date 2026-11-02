namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
public sealed class IntegrationTestAssembly
{
    [AssemblyInitialize]
    public static void Initialize(TestContext context)
    {
        KeycloakFixture.Initialize();
    }

    [AssemblyCleanup]
    public static Task CleanupAsync()
    {
        return KeycloakFixture.DisposeSharedAsync();
    }
}
