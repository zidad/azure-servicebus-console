namespace ServiceBusConsole.Models;

public record MessageSource(string EntityName, bool IsDlq, string? TopicName = null, string? SubscriptionName = null)
{
    public bool IsSubscription => TopicName is not null && SubscriptionName is not null;

    /// <summary>Where resent messages go: the queue itself, or the topic for a subscription.</summary>
    public string SendTarget => TopicName ?? EntityName;

    public string DisplayName => (IsSubscription ? $"{TopicName}/{SubscriptionName}" : EntityName) + (IsDlq ? " DLQ" : "");
}
