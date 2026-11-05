using System.Text.Json.Nodes;

namespace ServiceBusConsole.Abstractions;

/// <summary>A message as JSON; the body is text, or embedded as JSON when it is a JSON object or array.</summary>
public static class MessageJson
{
    public static string Format(MessageInfo message) => ToNode(message).ToJsonString(ContextText.Indented);

    public static JsonObject ToNode(MessageInfo message) => new()
    {
        ["sequenceNumber"] = message.SequenceNumber,
        ["messageId"] = message.MessageId,
        ["state"] = message.State.ToString(),
        ["subject"] = message.Subject,
        ["contentType"] = message.ContentType,
        ["enqueuedTime"] = ContextText.Time(message.EnqueuedTime),
        ["deliveryCount"] = message.DeliveryCount,
        ["deadLetterReason"] = message.DeadLetterReason,
        ["deadLetterErrorDescription"] = message.DeadLetterErrorDescription,
        ["properties"] = Properties(message.Properties),
        ["body"] = ContextText.JsonBody(message) ?? JsonValue.Create(message.BodyText),
    };

    // Indexer, not Add: a repeated name keeps the last value instead of throwing.
    private static JsonObject Properties(IEnumerable<KeyValuePair<string, string>> properties) =>
        properties.Aggregate(new JsonObject(), (node, p) =>
        {
            node[p.Key] = p.Value;
            return node;
        });
}
