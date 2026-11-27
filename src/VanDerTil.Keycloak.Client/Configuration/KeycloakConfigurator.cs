using VanDerTil.Keycloak.Client.Client;

namespace VanDerTil.Keycloak.Client.Configuration;

internal sealed class KeycloakConfigurator : IKeycloakConfigurator
{
    private readonly RealmReconciler _realmReconciler;
    private readonly ClientReconciler _clientReconciler;
    private readonly ClientScopeReconciler _scopeReconciler;
    private readonly GroupReconciler _groupReconciler;
    private readonly UserReconciler _userReconciler;
    private readonly OpenIdConnectAccessTokenProvider _accessTokenProvider;

    public KeycloakConfigurator(RefitClientFactory clientFactory, OpenIdConnectAccessTokenProvider accessTokenProvider)
    {
        ArgumentNullException.ThrowIfNull(clientFactory);
        ArgumentNullException.ThrowIfNull(accessTokenProvider);

        _realmReconciler = new RealmReconciler(clientFactory);
        _clientReconciler = new ClientReconciler(clientFactory);
        _scopeReconciler = new ClientScopeReconciler(clientFactory);
        _groupReconciler = new GroupReconciler(clientFactory);
        _userReconciler = new UserReconciler(clientFactory);
        _accessTokenProvider = accessTokenProvider;
    }

    public async Task ConfigureAsync(Action<KeycloakConfigurationBuilder> configure, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(configure);

        var accessToken = await _accessTokenProvider.GetAccessTokenAsync(cancellationToken).ConfigureAwait(false);

        var builder = new KeycloakConfigurationBuilder();
        configure(builder);

        var configuration = builder.Build();
        foreach (var realm in configuration.Realms)
        {
            await ConfigureRealmAsync(realm, accessToken, cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task ConfigureRealmAsync(RealmConfiguration realm, string accessToken, CancellationToken cancellationToken)
    {
        await _realmReconciler.EnsureRealmAsync(realm, accessToken, cancellationToken).ConfigureAwait(false);
        await _realmReconciler.EnsureRolesAsync(realm.RealmId, realm.Roles, accessToken, cancellationToken).ConfigureAwait(false);
        var resolved = new ResolvedRealmResources();
        resolved = await _clientReconciler.ReconcileAsync(realm.RealmId, realm.Clients, accessToken, resolved, cancellationToken).ConfigureAwait(false);
        await _scopeReconciler.ReconcileAsync(realm.RealmId, realm.Clients, accessToken, resolved, cancellationToken).ConfigureAwait(false);
        resolved = await _groupReconciler.ReconcileAsync(realm.RealmId, realm.Groups, accessToken, resolved, cancellationToken).ConfigureAwait(false);
        await _userReconciler.ReconcileAsync(realm.RealmId, realm.Users, accessToken, resolved, cancellationToken).ConfigureAwait(false);
    }
}
