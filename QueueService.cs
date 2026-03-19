using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class QueueService(ServiceBusConnection connection, ILogger<QueueService> logger)
{
    public async Task<List<QueueInfo>> GetQueuesAsync()
    {
        logger.LogInformation("Listing queues");
        var queues = new List<QueueInfo>();

        await foreach (var page in connection.AdminClient.GetQueuesAsync().AsPages())
        {
            foreach (var queue in page.Values)
            {
                var runtime = await connection.AdminClient.GetQueueRuntimePropertiesAsync(queue.Name);
                logger.LogDebug("Queue {Name}: Active={Active}, DLQ={Dlq}", queue.Name, runtime.Value.ActiveMessageCount, runtime.Value.DeadLetterMessageCount);
                queues.Add(new QueueInfo
                {
                    Name = queue.Name,
                    ActiveMessageCount = runtime.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount,
                    TransferMessageCount = runtime.Value.TransferMessageCount
                });
            }
        }

        logger.LogInformation("Found {Count} queues", queues.Count);
        return queues;
    }
}
