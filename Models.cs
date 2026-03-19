namespace ServiceBusConsole;

public record MessageSource(string EntityName, bool IsDlq, string? TopicName = null, string? SubscriptionName = null)
{
    public bool IsSubscription => TopicName is not null && SubscriptionName is not null;
}

public class NamespaceInfo
{
    public required string Name { get; set; }
    public required string FullyQualifiedNamespace { get; set; }
    public required string ResourceGroup { get; set; }
    public required string Location { get; set; }
}

public class QueueInfo
{
    public required string Name { get; set; }
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
}

public class TopicInfo
{
    public required string Name { get; set; }
    public int SubscriptionCount { get; set; }
    public long ScheduledMessageCount { get; set; }
}

public class SubscriptionInfo
{
    public required string Name { get; set; }
    public string TopicName { get; set; } = "";
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long TransferMessageCount { get; set; }

    public long TotalMessageCount => ActiveMessageCount + DeadLetterMessageCount + TransferMessageCount;
}

public class MessageInfo
{
    public required string MessageId { get; set; }
    public long SequenceNumber { get; set; }
    public DateTimeOffset EnqueuedTime { get; set; }
    public string ContentType { get; set; } = "";
    public string Subject { get; set; } = "";
    public string Body { get; set; } = "";
    public int DeliveryCount { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new();
}
