using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class MessageService(ServiceBusConnection connection, ILogger<MessageService> logger)
{
    public async Task<List<MessageInfo>> PeekMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        logger.LogInformation("Peeking {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = connection.BusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.PeekLock,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

        var messages = await receiver.PeekMessagesAsync(count);
        logger.LogInformation("Peeked {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

    public async Task<List<MessageInfo>> ReceiveMessagesAsync(string queueName, int count, bool fromDlq = false)
    {
        logger.LogWarning("Receiving (destructive) {Count} messages from {Queue} (DLQ={Dlq})", count, queueName, fromDlq);
        await using var receiver = connection.BusClient.CreateReceiver(queueName, new ServiceBusReceiverOptions
        {
            ReceiveMode = ServiceBusReceiveMode.ReceiveAndDelete,
            SubQueue = fromDlq ? SubQueue.DeadLetter : SubQueue.None
        });

        var messages = await receiver.ReceiveMessagesAsync(count, TimeSpan.FromSeconds(5));
        logger.LogInformation("Received {Count} messages", messages.Count);
        return messages.Select(MapMessage).ToList();
    }

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
