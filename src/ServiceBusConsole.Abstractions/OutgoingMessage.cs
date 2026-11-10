namespace ServiceBusConsole.Abstractions;

/// <summary>A new message to send; empty ids and names are left unset.</summary>
public sealed record OutgoingMessage
{
    public required ReadOnlyMemory<byte> Body { get; init; }
    public string MessageId { get; init; } = "";
    public string ContentType { get; init; } = "";
    public string Subject { get; init; } = "";
    public string CorrelationId { get; init; } = "";
    public IReadOnlyList<KeyValuePair<string, string>> Properties { get; init; } = [];
}
