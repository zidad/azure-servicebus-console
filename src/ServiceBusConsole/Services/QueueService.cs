using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services;

public class QueueService(ServiceBusConnection connection, ILogger<QueueService> logger) : IQueueService
{
    // Emulator only: two peeks per queue, run a few at a time.
    private const int MaxConcurrency = 8;

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");

        if (connection.IsEmulator)
        {
            await foreach (var queue in AsyncParallel.MapAsync(
                EmulatorQueueNames(), GetEmulatorQueueInfoAsync, MaxConcurrency, ct))
                yield return queue;
            yield break;
        }

        // One call per page of queues, so polling stays cheap.
        await foreach (var runtime in connection.AdminClient.GetQueuesRuntimePropertiesAsync(ct))
            yield return new QueueInfo
            {
                Name = runtime.Name,
                ActiveMessageCount = runtime.ActiveMessageCount,
                DeadLetterMessageCount = runtime.DeadLetterMessageCount,
                ScheduledMessageCount = runtime.ScheduledMessageCount,
                TransferMessageCount = runtime.TransferMessageCount,
            };
    }

    public async Task DeleteQueueAsync(string queueName)
    {
        logger.LogWarning("Deleting queue {Queue}", queueName);
        if (connection.IsEmulator)
            throw new NotSupportedException("Delete queue is not supported by the local emulator.");
        await connection.AdminClient.DeleteQueueAsync(queueName);
    }

    private static IAsyncEnumerable<string> EmulatorQueueNames() =>
        (EmulatorConfigReader.TryLoad()
            ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory."))
        .Queues.Select(q => q.Name).ToAsyncEnumerable();

    // The emulator has no management API: count by peeking.
    private async Task<QueueInfo> GetEmulatorQueueInfoAsync(string queueName, CancellationToken ct)
    {
        var (active, dlq) = await PeekQueueCountsAsync(queueName, ct);
        return new QueueInfo { Name = queueName, ActiveMessageCount = active, DeadLetterMessageCount = dlq };
    }

    private async Task<(long active, long dlq)> PeekQueueCountsAsync(string queue, CancellationToken ct)
    {
        long active = 0, dlq = 0;
        try
        {
            await using var r = connection.BusClient.CreateReceiver(queue);
            active = (await r.PeekMessagesAsync(1000, cancellationToken: ct)).Count;
        }
        catch (Exception ex) { logger.LogDebug("Peek active {Queue}: {Error}", queue, ex.Message); }
        try
        {
            await using var r = connection.BusClient.CreateReceiver(queue,
                new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
            dlq = (await r.PeekMessagesAsync(1000, cancellationToken: ct)).Count;
        }
        catch (Exception ex) { logger.LogDebug("Peek DLQ {Queue}: {Error}", queue, ex.Message); }
        return (active, dlq);
    }
}
