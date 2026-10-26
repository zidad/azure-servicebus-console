namespace ServiceBusConsole.Models;

public class QueueInfo
{
    public required string Name { get; set; }
    public long ActiveMessageCount { get; set; }
    public long DeadLetterMessageCount { get; set; }
    public long ScheduledMessageCount { get; set; }
    public long TransferMessageCount { get; set; }
    public bool IsRefreshing { get; set; }
}
