namespace ServiceBusConsole.Abstractions;

public sealed record TopicInfo
{
    public required string Name { get; init; }
    public int SubscriptionCount { get; init; }
    public long ScheduledMessageCount { get; init; }

    /// <summary>From the cache, while the live listing is still on its way.</summary>
    public bool IsRefreshing { get; init; }
}
