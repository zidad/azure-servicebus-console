namespace ServiceBusConsole.Models;

public class SubscriptionInfo
{
    public required string Name { get; set; }
    public string TopicName { get; set; } = "";
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
    public bool IsRefreshing { get; set; }
}
