using System.Collections.Concurrent;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>
/// Messages taken without acknowledging them: the channel holds them until each is settled, and closing it gives the
/// rest back. If the broker closes the channel (a consumer timeout, the connection lost), every held message is lost:
/// the broker puts it back for other consumers.
/// </summary>
internal sealed class LockedMessages : ILockedMessages
{
    private readonly Channel<MessageInfo> _held = Channel.CreateUnbounded<MessageInfo>();
    private readonly Channel<MessageKey> _lockLost = Channel.CreateUnbounded<MessageKey>();
    private readonly ConcurrentDictionary<MessageKey, ulong> _holding = new();
    // An AMQP channel takes one operation at a time.
    private readonly SemaphoreSlim _gate = new(1);
    private readonly CancellationTokenSource _cts = new();
    private readonly ILogger _logger;
    private readonly Task<IChannel> _channel;
    private Task _receive = Task.CompletedTask;

    private LockedMessages(AmqpConnection amqp, ILogger logger)
    {
        _logger = logger;
        _channel = OpenAsync(amqp);
    }

    public ChannelReader<MessageInfo> Held => _held.Reader;

    public ChannelReader<MessageKey> LockLost => _lockLost.Reader;

    /// <summary>Starts taking up to <paramref name="count"/> messages and returns at once; <see cref="Held"/> completes when the queue runs dry or <paramref name="count"/> are held.</summary>
    public static LockedMessages Start(AmqpConnection amqp, string queue, int count, ILogger logger, CancellationToken ct)
    {
        var locked = new LockedMessages(amqp, logger);
        locked._receive = locked.ReceiveAsync(queue, count, ct);
        return locked;
    }

    private async Task<IChannel> OpenAsync(AmqpConnection amqp)
    {
        var channel = await amqp.OpenChannelAsync(confirms: false, _cts.Token);
        channel.ChannelShutdownAsync += OnShutdownAsync;
        return channel;
    }

    private async Task ReceiveAsync(string queue, int count, CancellationToken ct)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linked.Token;
        Exception? error = null;
        try
        {
            var channel = await _channel;
            for (var n = 0; n < count && await GetAsync(channel, queue, token) is { } result; n++)
            {
                var message = RabbitMqMessageInfo.From(result, MessageState.Locked);
                _holding[message.Key] = result.DeliveryTag;
                await _held.Writer.WriteAsync(message, token);
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }
        _logger.LogInformation("Holding {Held} of {Requested} messages from {Queue}", _holding.Count, count, queue);
        _held.Writer.TryComplete(error);
    }

    private async Task<BasicGetResult?> GetAsync(IChannel channel, string queue, CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            return await channel.BasicGetAsync(queue, autoAck: false, ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    private Task OnShutdownAsync(object sender, ShutdownEventArgs args)
    {
        if (_holding.IsEmpty) return Task.CompletedTask;
        _logger.LogWarning("Channel closed with {Count} messages held: {Reason}", _holding.Count, args.ReplyText);
        foreach (var key in _holding.Keys)
            if (_holding.TryRemove(key, out _)) _lockLost.Writer.TryWrite(key);
        return Task.CompletedTask;
    }

    public bool IsHeld(MessageKey key) => _holding.ContainsKey(key);

    public Task CompleteAsync(MessageInfo message, CancellationToken ct = default) =>
        SettleAsync(message, (channel, tag) => channel.BasicAckAsync(tag, multiple: false, ct), ct);

    public Task AbandonAsync(MessageInfo message, CancellationToken ct = default) =>
        SettleAsync(message, (channel, tag) => channel.BasicNackAsync(tag, multiple: false, requeue: true, ct), ct);

    /// <summary>Rejects it: the queue's dead-letter exchange gets it, and without one it is dropped. AMQP carries no reason.</summary>
    public Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default) =>
        SettleAsync(message, (channel, tag) => channel.BasicRejectAsync(tag, requeue: false, ct), ct);

    private async Task SettleAsync(MessageInfo message, Func<IChannel, ulong, ValueTask> settle, CancellationToken ct)
    {
        if (!_holding.TryGetValue(message.Key, out var tag)) throw new MessageLockLostException(message.Key);
        var channel = await _channel;
        await _gate.WaitAsync(ct);
        try
        {
            if (!channel.IsOpen) throw new MessageLockLostException(message.Key);
            await settle(channel, tag);
            _holding.TryRemove(message.Key, out _);
        }
        finally
        {
            _gate.Release();
        }
    }

    public async ValueTask DisposeAsync()
    {
        await _cts.CancelAsync();
        await _receive;
        if (_channel is { IsCompletedSuccessfully: true, Result: var channel })
        {
            channel.ChannelShutdownAsync -= OnShutdownAsync;
            // Tag 0 with multiple: every message still held, back in order.
            if (channel.IsOpen && !_holding.IsEmpty)
            {
                _logger.LogInformation("Giving back {Count} held messages", _holding.Count);
                await channel.BasicNackAsync(0, multiple: true, requeue: true);
            }
            _holding.Clear();
            await channel.DisposeAsync();
        }
        _lockLost.Writer.TryComplete();
        _cts.Dispose();
        _gate.Dispose();
    }
}
