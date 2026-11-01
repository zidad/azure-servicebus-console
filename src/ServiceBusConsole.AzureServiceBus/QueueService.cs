using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Queues from the management API.</summary>
internal sealed class QueueService(ServiceBusAdministrationClient admin, ILogger<QueueService> logger) : IQueueService
{
    public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default)
    {
        logger.LogInformation("Listing queues");
        // One call per page of queues, so polling stays cheap.
        return admin.GetQueuesRuntimePropertiesAsync(ct).Select(runtime => new QueueInfo
        {
            Name = runtime.Name,
            ActiveMessageCount = runtime.ActiveMessageCount,
            DeadLetterMessageCount = runtime.DeadLetterMessageCount,
            ScheduledMessageCount = runtime.ScheduledMessageCount,
            TransferMessageCount = runtime.TransferMessageCount,
        });
    }
}
