using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Exceptions;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>Messages over AMQP: a channel per operation, publishes confirmed by the broker.</summary>
internal sealed class MessageService(AmqpConnection amqp, ILogger<MessageService> logger) : IMessageService, IPeekService
{
    private const int PageSize = 100;

    public PeekKind Kind => PeekKind.Requeues;

    /// <summary>Takes the messages without acknowledging them, then puts them all back in order: they come back redelivered.</summary>
    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = QueueOf(source);
        logger.LogInformation("Peeking {Count} from {Queue}", count, queue);
        await using var channel = await amqp.OpenChannelAsync(confirms: false, ct);
        try
        {
            await foreach (var page in GetAsync(channel, queue, count, autoAck: false, MessageState.Peeked, ct))
                yield return page;
        }
        finally
        {
            // Tag 0 with multiple: every message this channel holds.
            if (channel.IsOpen) await channel.BasicNackAsync(0, multiple: true, requeue: true, CancellationToken.None);
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(
        MessageSource source, int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = QueueOf(source);
        logger.LogWarning("Receiving and deleting {Count} from {Queue}", count, queue);
        await using var channel = await amqp.OpenChannelAsync(confirms: false, ct);
        await foreach (var page in GetAsync(channel, queue, count, autoAck: true, MessageState.Received, ct))
            yield return page;
    }

    public ILockedMessages StartReceiving(MessageSource source, int count, CancellationToken ct = default)
    {
        var queue = QueueOf(source);
        logger.LogInformation("Receiving {Count} from {Queue} unacknowledged", count, queue);
        return LockedMessages.Start(amqp, queue, count, logger, ct);
    }

    public async IAsyncEnumerable<int> SendCopiesAsync(
        MessageSource source, IReadOnlyList<MessageInfo> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = source.SendTarget;
        await using var channel = await amqp.OpenChannelAsync(confirms: true, ct);
        var sent = 0;
        foreach (var chunk in messages.Chunk(PageSize))
        {
            foreach (var message in chunk)
                await PublishAsync(channel, queue, RabbitMqMessageInfo.CopyOf(message), message.Body, ct);
            sent += chunk.Length;
            logger.LogInformation("Sent {Sent}/{Total} copies to {Queue}", sent, messages.Count, queue);
            yield return sent;
        }
    }

    public async IAsyncEnumerable<int> SendAsync(string queueOrTopic, IReadOnlyList<OutgoingMessage> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await using var channel = await amqp.OpenChannelAsync(confirms: true, ct);
        var sent = 0;
        foreach (var chunk in messages.Chunk(PageSize))
        {
            foreach (var message in chunk)
                await PublishAsync(channel, queueOrTopic, PropertiesOf(message), message.Body, ct);
            sent += chunk.Length;
            logger.LogInformation("Sent {Sent}/{Total} new messages to {Queue}", sent, messages.Count, queueOrTopic);
            yield return sent;
        }
    }

    public async IAsyncEnumerable<int> PurgeAsync(MessageSource source, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var queue = QueueOf(source);
        logger.LogWarning("Purging all messages from {Queue}", queue);
        await using var channel = await amqp.OpenChannelAsync(confirms: false, ct);
        var purged = await channel.QueuePurgeAsync(queue, ct);
        logger.LogInformation("Purged {Count} messages from {Queue}", purged, queue);
        yield return (int)purged;
    }

    private static async IAsyncEnumerable<IReadOnlyList<MessageInfo>> GetAsync(
        IChannel channel, string queue, int count, bool autoAck, MessageState state, [EnumeratorCancellation] CancellationToken ct)
    {
        var page = new List<MessageInfo>();
        for (var n = 0; n < count && await channel.BasicGetAsync(queue, autoAck, ct) is { } result; n++)
        {
            page.Add(RabbitMqMessageInfo.From(result, state));
            if (page.Count < PageSize) continue;
            yield return page;
            page = [];
        }
        if (page.Count > 0) yield return page;
    }

    // Straight to the queue through the default exchange; mandatory, so a missing queue fails instead of dropping it.
    private async Task PublishAsync(IChannel channel, string queue, BasicProperties properties, ReadOnlyMemory<byte> body, CancellationToken ct)
    {
        try
        {
            await channel.BasicPublishAsync("", queue, mandatory: true, properties, body, ct);
        }
        catch (PublishException e) when (e.IsReturn)
        {
            throw new InvalidOperationException(Strings.FormatMessageService_NoQueue(queue, amqp.Vhost), e);
        }
    }

    private static BasicProperties PropertiesOf(OutgoingMessage message) => new()
    {
        MessageId = Unset(message.MessageId),
        ContentType = Unset(message.ContentType),
        Type = Unset(message.Subject),
        CorrelationId = Unset(message.CorrelationId),
        Timestamp = new AmqpTimestamp(DateTimeOffset.UtcNow.ToUnixTimeSeconds()),
        DeliveryMode = DeliveryModes.Persistent,
        Headers = message.Properties.Count == 0 ? null : message.Properties.ToDictionary(p => p.Key, p => (object?)Encoding.UTF8.GetBytes(p.Value)),
    };

    private static string? Unset(string value) => value.Length == 0 ? null : value;

    // This broker has no topics, so every source is a queue.
    private static string QueueOf(MessageSource source) =>
        source.Match(q => q.Name, s => throw new ArgumentException(Strings.FormatMessageService_NotAQueue(s.DisplayName), nameof(source)));
}
