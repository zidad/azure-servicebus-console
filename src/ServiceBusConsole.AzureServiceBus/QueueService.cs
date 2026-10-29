using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

internal sealed class QueueService(ServiceBusConnection connection, ServiceBusClients clients, ILogger<QueueService> logger) : IQueueService
{
    // Emulator only: two peeks per queue, run a few at a time.
    private const int MaxConcurrency = 8;

    public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");
        return connection.IsEmulator
            ? AsyncParallel.MapAsync(
                EmulatorCounts.Config().Queues.Select(q => q.Name).ToAsyncEnumerable(), PeekQueueAsync, MaxConcurrency, ct)
            // One call per page of queues, so polling stays cheap.
            : clients.Admin.GetQueuesRuntimePropertiesAsync(ct).Select(runtime => new QueueInfo
            {
                Name = runtime.Name,
                ActiveMessageCount = runtime.ActiveMessageCount,
                DeadLetterMessageCount = runtime.DeadLetterMessageCount,
                ScheduledMessageCount = runtime.ScheduledMessageCount,
                TransferMessageCount = runtime.TransferMessageCount,
            });
    }

    private async Task<QueueInfo> PeekQueueAsync(string name, CancellationToken ct)
    {
        var (active, dlq) = await EmulatorCounts.PeekAsync(clients.Bus, name, null, logger, ct);
        return new QueueInfo { Name = name, ActiveMessageCount = active, DeadLetterMessageCount = dlq };
    }

    public async Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        logger.LogWarning("Deleting queue {Queue}", queueName);
        if (connection.IsEmulator)
            throw new NotSupportedException("Delete queue is not supported by the local emulator.");
        await clients.Admin.DeleteQueueAsync(queueName, ct);
    }
}
