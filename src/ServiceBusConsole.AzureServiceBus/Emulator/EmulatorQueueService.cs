using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

/// <summary>Queues from the emulator's config file, counts from peeking: it has no management API.</summary>
internal sealed class EmulatorQueueService(ServiceBusClient bus, ILogger<EmulatorQueueService> logger) : IQueueService
{
    // Two peeks per queue, run a few at a time.
    private const int MaxConcurrency = 8;

    public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");
        return AsyncParallel.MapAsync(EmulatorCounts.Config().Queues.Select(q => q.Name).ToAsyncEnumerable(), PeekQueueAsync, MaxConcurrency, ct);
    }

    private async Task<QueueInfo> PeekQueueAsync(string name, CancellationToken ct)
    {
        var (active, dlq) = await EmulatorCounts.PeekAsync(bus, name, null, logger, ct);
        return new QueueInfo { Name = name, ActiveMessageCount = active, DeadLetterMessageCount = dlq };
    }
}
