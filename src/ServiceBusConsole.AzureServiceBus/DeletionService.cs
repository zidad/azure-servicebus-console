using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

internal sealed class DeletionService(ServiceBusAdministrationClient admin, ILogger<DeletionService> logger) : IDeletionService
{
    public async Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        logger.LogWarning("Deleting queue {Queue}", queueName);
        await admin.DeleteQueueAsync(queueName, ct);
    }

    public async Task DeleteTopicAsync(string topicName, CancellationToken ct = default)
    {
        logger.LogWarning("Deleting topic {Topic}", topicName);
        await admin.DeleteTopicAsync(topicName, ct);
    }

    public async Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default)
    {
        logger.LogWarning("Deleting subscription {Subscription} from topic {Topic}", subscriptionName, topicName);
        await admin.DeleteSubscriptionAsync(topicName, subscriptionName, ct);
    }
}
