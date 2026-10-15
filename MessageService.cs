using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class MessageService(ServiceBusConnection connection, ILoggerFactory loggerFactory) : IMessageService
{
    private const int MaxBatch = 250;
    private readonly ILogger _logger = loggerFactory.CreateLogger<MessageService>();

    public async Task<List<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default)
    {
        _logger.LogInformation("Peeking {Count} from {Source} starting at {From}", count, source.DisplayName, fromSequenceNumber);
        await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.PeekLock);
        var result = new List<MessageInfo>(Math.Min(count, MaxBatch));
        var from = fromSequenceNumber;
        while (result.Count < count)
        {
            var batch = await receiver.PeekMessagesAsync(Math.Min(count - result.Count, MaxBatch), from, ct);
            if (batch.Count == 0) break;
            result.AddRange(batch.Select(m => new MessageInfo(m, MessageState.Peeked)));
            from = batch[^1].SequenceNumber + 1;
        }
        _logger.LogInformation("Peeked {Count} messages", result.Count);
        return result;
    }

    public async Task<List<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default)
    {
        _logger.LogWarning("Receiving and deleting {Count} from {Source}", count, source.DisplayName);
        await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.ReceiveAndDelete);
        var messages = await ReceiveUpToAsync(receiver, count, ct);
        _logger.LogInformation("Received and deleted {Count} messages", messages.Count);
        return messages.Select(m => new MessageInfo(m, MessageState.Received)).ToList();
    }

    public async Task<LockedMessages> ReceiveLockedAsync(MessageSource source, int count, CancellationToken ct = default)
    {
        _logger.LogInformation("Receiving {Count} with PeekLock from {Source}", count, source.DisplayName);
        var receiver = CreateReceiver(source, ServiceBusReceiveMode.PeekLock);
        try
        {
            var messages = await ReceiveUpToAsync(receiver, count, ct);
            _logger.LogInformation("Locked {Count} messages", messages.Count);
            return new LockedMessages(receiver, messages, loggerFactory.CreateLogger<LockedMessages>());
        }
        catch
        {
            await receiver.DisposeAsync();
            throw;
        }
    }

    public async Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default)
    {
        await using var sender = connection.BusClient.CreateSender(source.SendTarget);
        var sent = 0;
        while (sent < messages.Count)
        {
            using var batch = await sender.CreateMessageBatchAsync(ct);
            var added = 0;
            while (sent + added < messages.Count && batch.TryAddMessage(new ServiceBusMessage(messages[sent + added].Raw)))
                added++;
            if (added == 0)
                throw new InvalidOperationException($"Message #{messages[sent].SequenceNumber} is too large to send");
            await sender.SendMessagesAsync(batch, ct);
            sent += added;
            _logger.LogInformation("Sent {Sent}/{Total} copies to {Target}", sent, messages.Count, source.SendTarget);
            onSent(sent);
        }
    }

    public async Task<int> PurgeMessagesAsync(MessageSource source)
    {
        _logger.LogWarning("Purging all messages from {Source}", source.DisplayName);
        await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.ReceiveAndDelete);
        var total = 0;
        while (true)
        {
            var batch = await receiver.ReceiveMessagesAsync(500, TimeSpan.FromSeconds(5));
            if (batch.Count == 0) break;
            total += batch.Count;
        }
        _logger.LogInformation("Purged {Count} messages from {Source}", total, source.DisplayName);
        return total;
    }

    // A single receive call may return fewer messages than are available, so keep asking
    // until the count is reached or a short wait comes back empty.
    private static async Task<List<ServiceBusReceivedMessage>> ReceiveUpToAsync(ServiceBusReceiver receiver, int count, CancellationToken ct)
    {
        var result = new List<ServiceBusReceivedMessage>(Math.Min(count, MaxBatch));
        while (result.Count < count)
        {
            var wait = TimeSpan.FromSeconds(result.Count == 0 ? 5 : 1);
            var batch = await receiver.ReceiveMessagesAsync(Math.Min(count - result.Count, MaxBatch), wait, ct);
            if (batch.Count == 0) break;
            result.AddRange(batch);
        }
        return result;
    }

    private ServiceBusReceiver CreateReceiver(MessageSource source, ServiceBusReceiveMode mode)
    {
        var options = new ServiceBusReceiverOptions
        {
            ReceiveMode = mode,
            SubQueue = source.IsDlq ? SubQueue.DeadLetter : SubQueue.None,
        };
        return source is { TopicName: { } topic, SubscriptionName: { } subscription }
            ? connection.BusClient.CreateReceiver(topic, subscription, options)
            : connection.BusClient.CreateReceiver(source.EntityName, options);
    }
}
