namespace ServiceBusConsole.Abstractions;

public sealed record SubscriptionInfo
{
    public required string Name { get; init; }
    public string TopicName { get; init; } = "";
    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
    public long TransferMessageCount { get; init; }

    /// <summary>Last send or receive; null where unknown (the emulator).</summary>
    public DateTimeOffset? AccessedAt { get; init; }

    /// <summary>From the cache, while the live listing is still on its way.</summary>
    public bool IsRefreshing { get; init; }
}
