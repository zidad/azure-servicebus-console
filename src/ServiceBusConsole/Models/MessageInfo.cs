using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.Models;

/// <summary>A received or peeked message and how this console holds it.</summary>
public sealed record MessageInfo(ServiceBusReceivedMessage Raw, MessageState State)
{
    public string MessageId => Raw.MessageId;
    public long SequenceNumber => Raw.SequenceNumber;
    public DateTimeOffset EnqueuedTime => Raw.EnqueuedTime;
    public string ContentType => Raw.ContentType ?? "";
    public string Subject => Raw.Subject ?? "";
    public string Body => Raw.Body?.ToString() ?? "";
    public int DeliveryCount => Raw.DeliveryCount;
    public string? DeadLetterReason => Raw.DeadLetterReason;
    public IEnumerable<KeyValuePair<string, string>> Properties =>
        Raw.ApplicationProperties.Select(kvp => KeyValuePair.Create(kvp.Key, kvp.Value?.ToString() ?? ""));
}
