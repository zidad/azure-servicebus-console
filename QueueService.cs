using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class QueueService(ServiceBusConnection connection, ILogger<QueueService> logger) : IQueueService
{
    // See TopicService: one round-trip per queue, run a few at a time.
    private const int MaxConcurrency = 8;

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");

        await foreach (var queue in AsyncParallel.MapAsync(
            GetQueueNamesAsync(ct), GetQueueInfoAsync, MaxConcurrency, ct))
            yield return queue;
    }

    public async Task DeleteQueueAsync(string queueName)
    {
        logger.LogWarning("Deleting queue {Queue}", queueName);
        if (connection.IsEmulator)
            throw new NotSupportedException("Delete queue is not supported by the local emulator.");
        await connection.AdminClient.DeleteQueueAsync(queueName);
    }

    private async IAsyncEnumerable<string> GetQueueNamesAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct)
    {
        if (connection.IsEmulator)
        {
            var config = EmulatorConfigReader.TryLoad()
                ?? throw new InvalidOperationException("emulator/Config.json not found. Run from the project directory.");

            foreach (var q in config.Queues)
            {
                ct.ThrowIfCancellationRequested();
                yield return q.Name;
            }
            yield break;
        }

        await foreach (var page in connection.AdminClient.GetQueuesAsync().AsPages().WithCancellation(ct))
            foreach (var queue in page.Values)
            {
                ct.ThrowIfCancellationRequested();
                yield return queue.Name;
            }
    }

    private async Task<QueueInfo> GetQueueInfoAsync(string queueName, CancellationToken ct)
    {
        if (connection.IsEmulator)
        {
            var (active, dlq) = await PeekQueueCountsAsync(queueName, ct);
            return new QueueInfo { Name = queueName, ActiveMessageCount = active, DeadLetterMessageCount = dlq };
        }

        var runtime = await connection.AdminClient.GetQueueRuntimePropertiesAsync(queueName, ct);
        return new QueueInfo
        {
            Name = queueName,
            ActiveMessageCount = runtime.Value.ActiveMessageCount,
            DeadLetterMessageCount = runtime.Value.DeadLetterMessageCount,
            ScheduledMessageCount = runtime.Value.ScheduledMessageCount,
            TransferMessageCount = runtime.Value.TransferMessageCount
        };
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
