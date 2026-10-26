using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole;

public record MessageSource(string EntityName, bool IsDlq, string? TopicName = null, string? SubscriptionName = null)
{
    public bool IsSubscription => TopicName is not null && SubscriptionName is not null;

    /// <summary>Where resent messages go: the queue itself, or the topic for a subscription.</summary>
    public string SendTarget => TopicName ?? EntityName;

    public string DisplayName => (IsSubscription ? $"{TopicName}/{SubscriptionName}" : EntityName) + (IsDlq ? " DLQ" : "");
}

public class NamespaceInfo
{
    public required string Name { get; set; }
    public required string FullyQualifiedNamespace { get; set; }
    public required string ResourceGroup { get; set; }
    public required string Location { get; set; }
    // Null for the emulator and for namespaces cached before these were recorded.
    public string? Sku { get; set; }
    public string? Status { get; set; }
}

public class QueueInfo
{
    public required string Name { get; set; }
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
    public bool IsRefreshing { get; set; }
}

public class TopicInfo
{
    public required string Name { get; set; }
    public int SubscriptionCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public bool IsRefreshing { get; set; }
}

public class SubscriptionInfo
{
    public required string Name { get; set; }
    public string TopicName { get; set; } = "";
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
    public bool IsRefreshing { get; set; }

    public long TotalMessageCount => ActiveMessageCount + DeadLetterMessageCount + TransferMessageCount;
}

public enum MessageState { Peeked, Locked, Received }

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
