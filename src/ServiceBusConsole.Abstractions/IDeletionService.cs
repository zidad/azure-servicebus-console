namespace ServiceBusConsole.Abstractions;

public interface IDeletionService
{
    Task DeleteQueueAsync(string queueName, CancellationToken ct = default);
    Task DeleteTopicAsync(string topicName, CancellationToken ct = default);
    Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default);
}
