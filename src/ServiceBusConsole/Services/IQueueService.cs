namespace ServiceBusConsole.Services;

public interface IQueueService
{
    IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default);
    Task DeleteQueueAsync(string queueName);
}
