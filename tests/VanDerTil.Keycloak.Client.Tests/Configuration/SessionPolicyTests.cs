using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class SessionPolicyTests
{
    [TestMethod]
    public void Should_RejectInvalidDurationsAndRotationReuse()
    {
        var client = new ClientBuilder("web");
        var realm = new RealmBuilder("app");
        foreach (var invalid in new[] { TimeSpan.Zero, TimeSpan.FromSeconds(-1), TimeSpan.FromMilliseconds(1), TimeSpan.MaxValue })
        {
            Should.Throw<ArgumentOutOfRangeException>(() => client.WithAccessTokenLifespan(invalid));
            Should.Throw<ArgumentOutOfRangeException>(() => client.WithClientSessionIdleTimeout(invalid));
            Should.Throw<ArgumentOutOfRangeException>(() => client.WithClientSessionMaxLifespan(invalid));
            Should.Throw<ArgumentOutOfRangeException>(() => realm.WithSsoSessionIdleTimeout(invalid));
            Should.Throw<ArgumentOutOfRangeException>(() => realm.WithSsoSessionMaxLifespan(invalid));
        }
        Should.Throw<ArgumentOutOfRangeException>(() => realm.WithRefreshTokenRotation(true, -1));
    }

    [TestMethod]
    public void Should_LeaveUnconfiguredPolicyUnspecified()
    {
        var client = new ClientBuilder("web").Build();
        client.FullScopeAllowed.ShouldBeNull();
        client.Attributes.ShouldBeNull();
        var realm = new RealmBuilder("app").Build();
        realm.RevokeRefreshToken.ShouldBeNull();
        realm.RefreshTokenMaxReuse.ShouldBeNull();
        realm.SsoSessionIdleTimeout.ShouldBeNull();
        realm.SsoSessionMaxLifespan.ShouldBeNull();
    }
}