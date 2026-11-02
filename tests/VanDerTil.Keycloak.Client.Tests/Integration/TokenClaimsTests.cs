namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class TokenClaimsTests
{
    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    [DataRow("none", "")]
    [DataRow("direct", "View")]
    [DataRow("group", "Manage,View")]
    public async Task Should_RestrictIdTokenRolesToAssignedClientRoles(string assignment, string expectedRoles)
    {
        // Arrange
        var signIn = await SignInScenario.CreateRoleClaimsAsync(_fixture, user =>
        {
            if (assignment == "direct")
            {
                user.InClientRole(SignInScenario.ClientId, "View");
            }
            if (assignment == "group")
            {
                user.InGroup("Developer");
            }
        });

        // Act
        using var tokens = await signIn.LoginAsync();

        // Assert
        SignInScenario.ReadRoles(tokens.RootElement.GetProperty("id_token").GetString()!)
            .ShouldBe(expectedRoles.Split(',', StringSplitOptions.RemoveEmptyEntries));
    }

    [TestMethod]
    public async Task Should_ApplyConfiguredAccessTokenLifespan()
    {
        // Arrange
        var signIn = await SignInScenario.CreateAsync(_fixture,
            configureClient: client => client.WithAccessTokenLifespan(TimeSpan.FromMinutes(5)));

        // Act
        using var tokens = await signIn.LoginAsync();

        // Assert
        using var payload = SignInScenario.ReadTokenPayload(tokens.RootElement.GetProperty("access_token").GetString()!);
        var issuedAt = payload.RootElement.GetProperty("iat").GetInt64();
        var expiresAt = payload.RootElement.GetProperty("exp").GetInt64();
        (expiresAt - issuedAt).ShouldBe(300);
    }
}