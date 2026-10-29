namespace ServiceBusConsole.Abstractions;

public interface IQueueService
{
    IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default);
    Task DeleteQueueAsync(string queueName, CancellationToken ct = default);
}
