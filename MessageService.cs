using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class MessageService(ServiceBusConnection connection, ILogger<MessageService> logger) : IMessageService
{
    public async Task<List<MessageInfo>> PeekMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        logger.LogInformation("Peeking {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = CreateQueueReceiver(queueName, ServiceBusReceiveMode.PeekLock, fromDlq);
        var messages = await receiver.PeekMessagesAsync(count);
        logger.LogInformation("Peeked {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageInfo>> ReceiveMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        logger.LogWarning("Receiving (destructive) {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = CreateQueueReceiver(queueName, ServiceBusReceiveMode.ReceiveAndDelete, fromDlq);
        var messages = await receiver.ReceiveMessagesAsync(count, TimeSpan.FromSeconds(5));
        logger.LogInformation("Received {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageInfo>> PeekSubscriptionMessagesAsync(string topicName, string subscriptionName, int count, bool fromDlq = false)
    {
        logger.LogInformation("Peeking {Count} messages from {Topic}/{Sub} (DLQ={Dlq})", count, topicName, subscriptionName, fromDlq);
        await using var receiver = CreateSubscriptionReceiver(topicName, subscriptionName, ServiceBusReceiveMode.PeekLock, fromDlq);
        var messages = await receiver.PeekMessagesAsync(count);
        logger.LogInformation("Peeked {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageInfo>> ReceiveSubscriptionMessagesAsync(string topicName, string subscriptionName, int count, bool fromDlq = false)
    {
        logger.LogWarning("Receiving (destructive) {Count} from {Topic}/{Sub} (DLQ={Dlq})", count, topicName, subscriptionName, fromDlq);
        await using var receiver = CreateSubscriptionReceiver(topicName, subscriptionName, ServiceBusReceiveMode.ReceiveAndDelete, fromDlq);
        var messages = await receiver.ReceiveMessagesAsync(count, TimeSpan.FromSeconds(5));
        logger.LogInformation("Received {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task DeleteMessageAsync(MessageSource source, long sequenceNumber)
    {
        logger.LogWarning("Deleting message #{SeqNum} from {Entity} (DLQ={Dlq})", sequenceNumber, source.EntityName, source.IsDlq);

        await using var receiver = source.IsSubscription
            ? CreateSubscriptionReceiver(source.TopicName!, source.SubscriptionName!, ServiceBusReceiveMode.PeekLock, source.IsDlq)
            : CreateQueueReceiver(source.EntityName, ServiceBusReceiveMode.PeekLock, source.IsDlq);

        const int batchSize = 50;
        const int maxMessages = 1000;
        var scanned = 0;

        while (scanned < maxMessages)
        {
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(3));
            if (batch.Count == 0) break;

            foreach (var msg in batch)
            {
                if (msg.SequenceNumber == sequenceNumber)
                {
                    await receiver.CompleteMessageAsync(msg);
                    logger.LogInformation("Deleted message #{SeqNum}", sequenceNumber);
                    return;
                }

                await receiver.AbandonMessageAsync(msg);
            }

            scanned += batch.Count;
        }

        throw new InvalidOperationException($"Message #{sequenceNumber} not found in {source.EntityName}");
    }

    public async Task RequeueMessageAsync(MessageSource source, long sequenceNumber)
    {
        logger.LogWarning("Requeuing message #{SeqNum} from DLQ {Entity}", sequenceNumber, source.EntityName);

        await using var receiver = source.IsSubscription
            ? CreateSubscriptionReceiver(source.TopicName!, source.SubscriptionName!, ServiceBusReceiveMode.PeekLock, true)
            : CreateQueueReceiver(source.EntityName, ServiceBusReceiveMode.PeekLock, true);

        var destination = source.IsSubscription ? source.TopicName! : source.EntityName;
        await using var sender = connection.BusClient.CreateSender(destination);

        const int batchSize = 50;
        const int maxMessages = 1000;
        var scanned = 0;

        while (scanned < maxMessages)
        {
            var batch = await receiver.ReceiveMessagesAsync(batchSize, TimeSpan.FromSeconds(3));
            if (batch.Count == 0) break;

            foreach (var msg in batch)
            {
                if (msg.SequenceNumber == sequenceNumber)
                {
                    await sender.SendMessageAsync(new ServiceBusMessage(msg));
                    await receiver.CompleteMessageAsync(msg);
                    logger.LogInformation("Requeued message #{SeqNum} to {Destination}", sequenceNumber, destination);
                    return;
                }

                await receiver.AbandonMessageAsync(msg);
            }

            scanned += batch.Count;
        }

        throw new InvalidOperationException($"Message #{sequenceNumber} not found in DLQ of {source.EntityName}");
    }

    private ServiceBusReceiver CreateQueueReceiver(string queueName, ServiceBusReceiveMode mode, bool fromDlq) =>
        connection.BusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = mode,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

    private ServiceBusReceiver CreateSubscriptionReceiver(string topicName, string subscriptionName, ServiceBusReceiveMode mode, bool fromDlq) =>
        connection.BusClient.CreateReceiver(topicName, subscriptionName, new ServiceBusReceiverOptions
        {
            ReceiveMode = mode,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

    private static MessageInfo MapMessage(ServiceBusReceivedMessage m) => new()
    {
        MessageId = m.MessageId,
        SequenceNumber = m.SequenceNumber,
        EnqueuedTime = m.EnqueuedTime,
        ContentType = m.ContentType ?? "",
        Subject = m.Subject ?? "",
        Body = m.Body?.ToString() ?? "",
        DeliveryCount = m.DeliveryCount,
        Properties = m.ApplicationProperties.ToDictionary(
            kvp => kvp.Key,
            kvp => kvp.Value?.ToString() ?? "")
    };
}
