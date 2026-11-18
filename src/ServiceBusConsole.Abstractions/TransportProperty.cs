using System.Text.Json.Nodes;

namespace ServiceBusConsole.Abstractions;

/// <summary>A message field only some transports have (Service Bus: the sequence number), shown beside the common ones.</summary>
/// <param name="Id">Its name in JSON and templates: <c>sequenceNumber</c>.</param>
/// <param name="Label">What the UI calls it.</param>
/// <param name="Value">A JSON value, so a number stays a number in JSON output.</param>
public sealed record TransportProperty(string Id, string Label, JsonValue Value)
{
    public string Text => Value.ToString();
}
