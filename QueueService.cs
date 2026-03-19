using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class QueueService(ServiceBusConnection connection, ILogger<QueueService> logger) : IQueueService
{
    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");

        await foreach (var page in connection.AdminClient.GetQueuesAsync().AsPages().WithCancellation(ct))
        {
            foreach (var queue in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                var runtime = await connection.AdminClient.GetQueueRuntimePropertiesAsync(queue.Name, ct);
                logger.LogDebug("Queue {Name}: Active={Active}, DLQ={Dlq}",
                    queue.Name, runtime.Value.ActiveMessageCount, runtime.Value.DeadLetterMessageCount);

                yield return new QueueInfo
                {
                    Name = queue.Name,
                    ActiveMessageCount = runtime.Value.ActiveMessageCount,
                    DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
                    ScheduledMessageCount = runtime.Value.ScheduledMessageCount,
                    TransferMessageCount = runtime.Value.TransferMessageCount
                };
            }
        }
    }

    public async Task DeleteQueueAsync(string queueName)
    {
        logger.LogWarning("Deleting queue {Queue}", queueName);
        await connection.AdminClient.DeleteQueueAsync(queueName);
    }
}
