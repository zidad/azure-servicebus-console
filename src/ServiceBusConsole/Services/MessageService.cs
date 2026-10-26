using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services;

public sealed class MessageService(ServiceBusConnection connection, ILoggerFactory loggerFactory) : IMessageService
{
    private const int MaxBatch = 250;
    private readonly ILogger _logger = loggerFactory.CreateLogger<MessageService>();

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogInformation("Peeking {Count} from {Source} starting at {From}", count, source.DisplayName, fromSequenceNumber);
        await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.PeekLock);
        var peeked = 0;
        var from = fromSequenceNumber;
        while (peeked < count)
        {
            var batch = await receiver.PeekMessagesAsync(Math.Min(count - peeked, MaxBatch), from, ct);
            if (batch.Count == 0) break;
            peeked += batch.Count;
            from = batch[^1].SequenceNumber + 1;
            yield return [.. batch.Select(m => new MessageInfo(m, MessageState.Peeked))];
        }
        _logger.LogInformation("Peeked {Count} messages", peeked);
    }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        _logger.LogWarning("Receiving and deleting {Count} from {Source}", count, source.DisplayName);
        await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.ReceiveAndDelete);
        var received = 0;
        while (received < count)
        {
            // A receive may return fewer messages than exist: wait longer for the first.
            var wait = TimeSpan.FromSeconds(received == 0 ? 5 : 1);
            var batch = await receiver.ReceiveMessagesAsync(Math.Min(count - received, MaxBatch), wait, ct);
            if (batch.Count == 0) break;
            received += batch.Count;
            yield return [.. batch.Select(m => new MessageInfo(m, MessageState.Received))];
        }
        _logger.LogInformation("Received and deleted {Count} messages", received);
    }

    public ILockedMessages StartReceiving(MessageSource source, int count, System.Threading.Channels.ChannelWriter<ServiceBusReceivedMessage> received, CancellationToken ct = default)
    {
        _logger.LogInformation("Receiving {Count} with PeekLock from {Source}", count, source.DisplayName);
        // The returned LockedMessages owns the receiver and disposes it.
        var receiver = CreateReceiver(source, ServiceBusReceiveMode.PeekLock);
        return LockedMessages.StartReceive(receiver, count, received, loggerFactory.CreateLogger<LockedMessages>(), ct);
    }

    // Bounds how far Retry scans a dead-letter queue for the messages it was asked to lock.
    private const int MaxLockScan = 5000;

    public ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, System.Threading.Channels.ChannelWriter<ServiceBusReceivedMessage> found, CancellationToken ct = default)
    {
        _logger.LogInformation("Locking {Count} messages in {Source}", sequenceNumbers.Count, source.DisplayName);
        // The returned LockedMessages owns the receiver and disposes it.
        var receiver = CreateReceiver(source, ServiceBusReceiveMode.PeekLock);
        return LockedMessages.StartScan(receiver, sequenceNumbers, MaxLockScan, found, loggerFactory.CreateLogger<LockedMessages>(), ct);
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

    // Order does not matter when deleting everything, so several receivers drain in parallel.
    private const int PurgeReceivers = 4;

    public async Task<int> PurgeMessagesAsync(MessageSource source, Action<int> onPurged, CancellationToken ct = default)
    {
        _logger.LogWarning("Purging all messages from {Source}", source.DisplayName);
        var total = 0;
        try
        {
            await Parallel.ForEachAsync(Enumerable.Range(0, PurgeReceivers), ct, async (_, token) =>
            {
                await using var receiver = CreateReceiver(source, ServiceBusReceiveMode.ReceiveAndDelete);
                while (true)
                {
                    var batch = await receiver.ReceiveMessagesAsync(500, TimeSpan.FromSeconds(5), token);
                    if (batch.Count == 0) return;
                    onPurged(Interlocked.Add(ref total, batch.Count));
                }
            });
        }
        finally
        {
            _logger.LogInformation("Purged {Count} messages from {Source}", total, source.DisplayName);
        }
        return total;
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
