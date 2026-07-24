namespace VanDerTil.Keycloak.Client.Configuration;

public interface IKeycloakConfigurator
{
    Task ConfigureAsync(Action<KeycloakConfigurationBuilder> configure, string accessToken, CancellationToken cancellationToken);
}
