using System.Text.Json.Serialization;

namespace Forza6Client.Services;

[JsonSerializable(typeof(HelloPacket))]
[JsonSerializable(typeof(SettingsData))]
internal partial class JsonContext : JsonSerializerContext;

internal record HelloPacket(string type, string username, string markerColor, string client);

internal record SettingsData(string? Username, string? ListenHost, int ListenPort, string? MarkerColor);
