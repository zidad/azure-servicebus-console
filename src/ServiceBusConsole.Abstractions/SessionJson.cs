using System.Text.Json.Nodes;

namespace ServiceBusConsole.Abstractions;

/// <summary>A <see cref="SessionSnapshot"/> as JSON, camel case, absent parts as null.</summary>
public static class SessionJson
{
    public static string Format(SessionSnapshot snapshot) => ToNode(snapshot).ToJsonString(ContextText.Indented);

    public static JsonObject ToNode(SessionSnapshot snapshot) => new()
    {
        ["at"] = ContextText.Time(snapshot.At),
        ["subscription"] = snapshot.Subscription is { } s
            ? new JsonObject { ["name"] = s.Name, ["id"] = s.Id, ["isEmulator"] = s.IsEmulator }
            : null,
        ["namespace"] = snapshot.Namespace is { } ns
            ? new JsonObject
            {
                ["name"] = ns.Name,
                ["fullyQualifiedNamespace"] = ns.FullyQualifiedNamespace,
                ["resourceGroup"] = ns.ResourceGroup,
                ["location"] = ns.Location,
                ["sku"] = ns.Sku,
                ["status"] = ns.Status,
                ["resourceId"] = ns.ResourceId,
            }
            : null,
        ["source"] = Source(snapshot.Source),
        ["mode"] = snapshot.Mode,
        ["message"] = snapshot.Message is { } m ? MessageJson.ToNode(m) : null,
        ["activity"] = new JsonArray([.. snapshot.Activity.Select(a => (JsonNode)new JsonObject
        {
            ["at"] = ContextText.Time(a.At),
            ["kind"] = a.Kind.ToString(),
            ["text"] = a.Text,
            ["source"] = Source(a.Source),
        })]),
    };

    private static JsonObject? Source(MessageSource? source) => source?.Match(
        q => new JsonObject { ["kind"] = "queue", ["queue"] = q.Name, ["isDlq"] = q.IsDlq, ["displayName"] = q.DisplayName },
        s => new JsonObject { ["kind"] = "subscription", ["topic"] = s.Topic, ["subscription"] = s.Name, ["isDlq"] = s.IsDlq, ["displayName"] = s.DisplayName });
}
