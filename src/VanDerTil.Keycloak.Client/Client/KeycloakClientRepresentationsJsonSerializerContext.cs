using System.Text.Json;
using System.Text.Json.Serialization;

using VanDerTil.Keycloak.Client.Client.Representations;

namespace VanDerTil.Keycloak.Client.Client;

[JsonSourceGenerationOptions(JsonSerializerDefaults.Web)]
[JsonSerializable(typeof(ClientRepresentation))]
[JsonSerializable(typeof(List<ClientRepresentation>))]
[JsonSerializable(typeof(ClientSecretRepresentation))]
[JsonSerializable(typeof(List<ClientSecretRepresentation>))]
[JsonSerializable(typeof(CredentialRepresentation))]
[JsonSerializable(typeof(List<CredentialRepresentation>))]
[JsonSerializable(typeof(GroupRepresentation))]
[JsonSerializable(typeof(List<GroupRepresentation>))]
[JsonSerializable(typeof(ProtocolMapperRepresentation))]
[JsonSerializable(typeof(List<ProtocolMapperRepresentation>))]
[JsonSerializable(typeof(RealmRepresentation))]
[JsonSerializable(typeof(List<RealmRepresentation>))]
[JsonSerializable(typeof(RoleRepresentation))]
[JsonSerializable(typeof(List<RoleRepresentation>))]
[JsonSerializable(typeof(UserRepresentation))]
[JsonSerializable(typeof(List<UserRepresentation>))]
internal sealed partial class KeycloakClientRepresentationsJsonSerializerContext : JsonSerializerContext
{
}
