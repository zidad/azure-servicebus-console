using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.Models;

public class MessageInfo(ServiceBusReceivedMessage raw, MessageState state)
{
    public ServiceBusReceivedMessage Raw { get; } = raw;
    public MessageState State { get; } = state;

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
