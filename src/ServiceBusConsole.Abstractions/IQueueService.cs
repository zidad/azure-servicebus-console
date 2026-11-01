namespace ServiceBusConsole.Abstractions;

public interface IQueueService
{
    IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default);
}
