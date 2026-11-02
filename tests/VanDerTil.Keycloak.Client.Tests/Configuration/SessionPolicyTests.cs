using VanDerTil.Keycloak.Client.Configuration;

namespace VanDerTil.Keycloak.Client.Tests.Configuration;

[TestClass]
public sealed class SessionPolicyTests
{
    public static IEnumerable<object[]> InvalidDurations =>
    [
        new object[] { TimeSpan.Zero },
        new object[] { TimeSpan.FromSeconds(-1) },
        new object[] { TimeSpan.FromMilliseconds(1) },
        new object[] { TimeSpan.MaxValue },
    ];

    [TestMethod]
    [DynamicData(nameof(InvalidDurations))]
    public void Should_RejectInvalidAccessTokenLifespan(TimeSpan duration)
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.WithAccessTokenLifespan(duration);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidDurations))]
    public void Should_RejectInvalidClientSessionIdleTimeout(TimeSpan duration)
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.WithClientSessionIdleTimeout(duration);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidDurations))]
    public void Should_RejectInvalidClientSessionMaxLifespan(TimeSpan duration)
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        Action configure = () => builder.WithClientSessionMaxLifespan(duration);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidDurations))]
    public void Should_RejectInvalidSsoSessionIdleTimeout(TimeSpan duration)
    {
        // Arrange
        var builder = new RealmBuilder("app");

        // Act
        Action configure = () => builder.WithSsoSessionIdleTimeout(duration);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    [DynamicData(nameof(InvalidDurations))]
    public void Should_RejectInvalidSsoSessionMaxLifespan(TimeSpan duration)
    {
        // Arrange
        var builder = new RealmBuilder("app");

        // Act
        Action configure = () => builder.WithSsoSessionMaxLifespan(duration);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    public void Should_RejectNegativeRefreshTokenReuse()
    {
        // Arrange
        var builder = new RealmBuilder("app");

        // Act
        Action configure = () => builder.WithRefreshTokenRotation(true, -1);

        // Assert
        Should.Throw<ArgumentOutOfRangeException>(configure);
    }

    [TestMethod]
    public void Should_LeaveUnconfiguredClientPolicyUnspecified()
    {
        // Arrange
        var builder = new ClientBuilder("web");

        // Act
        var client = builder.Build();

        // Assert
        client.FullScopeAllowed.ShouldBeNull();
        client.Attributes.ShouldBeNull();
    }

    [TestMethod]
    public void Should_LeaveUnconfiguredRealmPolicyUnspecified()
    {
        // Arrange
        var builder = new RealmBuilder("app");

        // Act
        var realm = builder.Build();

        // Assert
        realm.RevokeRefreshToken.ShouldBeNull();
        realm.RefreshTokenMaxReuse.ShouldBeNull();
        realm.SsoSessionIdleTimeout.ShouldBeNull();
        realm.SsoSessionMaxLifespan.ShouldBeNull();
    }
}
