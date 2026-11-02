using System.Linq;
using System.Net;

namespace VanDerTil.Keycloak.Client.Tests.Integration;

[TestClass]
[TestCategory("Integration")]
public sealed class RefreshTokenTests
{
    private KeycloakFixture _fixture = null!;

    [TestInitialize]
    public async Task InitializeAsync()
    {
        _fixture = await KeycloakFixture.GetAsync();
    }

    [TestMethod]
    public async Task Should_RotateRefreshTokensAndRejectReusedTokens()
    {
        // Arrange
        var signIn = await SignInScenario.CreateAsync(_fixture,
            configureRealm: realm => realm.WithRefreshTokenRotation(true));
        using var original = await signIn.LoginAsync();
        var refreshToken = original.RootElement.GetProperty("refresh_token").GetString()!;

        // Act
        using var renewed = await signIn.RefreshTokensAsync(refreshToken);
        using var replay = await signIn.RequestRefreshAsync(refreshToken);

        // Assert
        renewed.RootElement.GetProperty("refresh_token").GetString().ShouldNotBe(refreshToken);
        replay.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [TestMethod]
    public async Task Should_ReflectChangedGroupRolesInRefreshedClaims()
    {
        // Arrange
        var signIn = await SignInScenario.CreateRoleClaimsAsync(_fixture, user => user.InGroup("Developer"));
        using var original = await signIn.LoginAsync();
        var refreshToken = original.RootElement.GetProperty("refresh_token").GetString()!;
        await _fixture.ConfigureRealmAsync(signIn.RealmId, realm =>
            realm.AddGroup("Developer", group => group.WithClientRoles(SignInScenario.ClientId, "View")));

        // Act
        using var renewed = await signIn.RefreshTokensAsync(refreshToken);
        using var userInfo = await signIn.GetUserInfoAsync(renewed.RootElement.GetProperty("access_token").GetString()!);

        // Assert
        SignInScenario.ReadRoles(renewed.RootElement.GetProperty("id_token").GetString()!).ShouldBe(new[] { "View" });
        userInfo.RootElement.GetProperty("roles").EnumerateArray().Select(role => role.GetString())
            .ShouldBe(new[] { "View" });
    }
}