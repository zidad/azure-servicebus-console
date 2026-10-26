namespace ServiceBusConsole.Models;

public class TopicInfo
{
    public required string Name { get; set; }
    public int SubscriptionCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public bool IsRefreshing { get; set; }
}
