namespace ServiceBusConsole.Abstractions;

public sealed record QueueInfo
{
    public required string Name { get; init; }
    public long ActiveMessageCount { get; init; }
    public long DeadLetterMessageCount { get; init; }
    public long ScheduledMessageCount { get; init; }
    public long TransferMessageCount { get; init; }

    /// <summary>From the cache, while the live listing is still on its way.</summary>
    public bool IsRefreshing { get; init; }
}
