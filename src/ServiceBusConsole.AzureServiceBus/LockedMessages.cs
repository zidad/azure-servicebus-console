using System.Collections.Concurrent;
using System.Threading.Channels;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>
/// Messages received in PeekLock mode. Locks belong to the receiver link, so the receiver stays
/// open until disposal, locks are renewed in the background, and whatever is still held on
/// disposal is abandoned so it becomes visible again right away instead of after lock expiry.
/// </summary>
internal sealed class LockedMessages : ILockedMessages
{
    // A scan is bounded, so a slow reader holds it back instead of it locking far ahead.
    private const int ScanAhead = 200;

    private readonly ServiceBusReceiver _receiver;
    private readonly ILogger _logger;
    private readonly Channel<MessageInfo> _arrivals;
    private readonly Channel<long> _lockLost = Channel.CreateUnbounded<long>();
    private readonly ConcurrentDictionary<long, HeldMessage> _held = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _renewLoop;
    private Task _scan = Task.CompletedTask;

    private sealed record HeldMessage(ServiceBusReceivedMessage Message, TimeSpan LockDuration);

    private LockedMessages(ServiceBusReceiver receiver, Channel<MessageInfo> arrivals, ILogger logger)
    {
        _receiver = receiver;
        _arrivals = arrivals;
        _logger = logger;
        _renewLoop = RenewLoopAsync(_cts.Token);
    }

    public ChannelReader<MessageInfo> Held => _arrivals.Reader;

    public ChannelReader<long> LockLost => _lockLost.Reader;

    /// <summary>
    /// Starts locking specific messages and returns at once. Service Bus can only lock by receiving from
    /// the front, so a background scan receives until every wanted message is held, the entity runs dry, or
    /// <paramref name="maxScan"/> messages were received. Each wanted message arrives on <see cref="Held"/>
    /// as soon as it is held; <see cref="Held"/> completes when the scan ends, with the error if it failed
    /// or was cancelled. Messages passed on the way are abandoned per batch: they stay, with delivery count + 1.
    /// </summary>
    public static LockedMessages StartScan(
        ServiceBusReceiver receiver, IReadOnlyCollection<long> sequenceNumbers, int maxScan, ILogger logger, CancellationToken ct = default)
    {
        var locked = new LockedMessages(receiver, Channel.CreateBounded<MessageInfo>(ScanAhead), logger);
        locked._scan = locked.ReceiveAsync(sequenceNumbers.ToHashSet(), maxScan, ct);
        return locked;
    }

    /// <summary>
    /// Starts receiving up to <paramref name="count"/> messages in PeekLock mode and returns at once; each is
    /// held and arrives on <see cref="Held"/>, which completes when receiving ends.
    /// </summary>
    public static LockedMessages StartReceive(ServiceBusReceiver receiver, int count, ILogger logger, CancellationToken ct = default)
    {
        var locked = new LockedMessages(receiver, Channel.CreateUnbounded<MessageInfo>(), logger);
        locked._scan = locked.ReceiveAsync(wanted: null, count, ct);
        return locked;
    }

    // wanted null: hold everything received (a plain PeekLock receive); otherwise hold only the wanted ones.
    private async Task ReceiveAsync(HashSet<long>? wanted, int max, CancellationToken ct)
    {
        var output = _arrivals.Writer;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, _cts.Token);
        var token = linked.Token;
        var requested = wanted?.Count ?? max;
        var received = 0;
        var held = 0;
        Exception? error = null;
        try
        {
            while ((wanted is null || wanted.Count > 0) && received < max)
            {
                // A receive may return fewer messages than exist: a plain receive waits longer for the first.
                var wait = TimeSpan.FromSeconds(wanted is not null ? 2 : received == 0 ? 5 : 1);
                var batch = await _receiver.ReceiveMessagesAsync(Math.Min(100, max - received), wait, token);
                if (batch.Count == 0) break;
                received += batch.Count;
                var hits = wanted is null ? batch.ToList() : batch.Where(m => wanted.Contains(m.SequenceNumber)).ToList();
                wanted?.ExceptWith(hits.Select(m => m.SequenceNumber));
                if (hits.Count < batch.Count)
                    await AbandonAllAsync(_receiver, [.. batch.Except(hits)], _logger);
                Hold(hits);
                held += hits.Count;
                foreach (var m in hits)
                    await output.WriteAsync(ServiceBusMessageInfo.From(m, MessageState.Locked), token);
            }
        }
        catch (Exception ex)
        {
            error = ex;
        }

        _logger.LogInformation("Locked {Held} of {Requested} messages after receiving {Received}", held, requested, received);
        output.TryComplete(error);
    }

    private void Hold(IEnumerable<ServiceBusReceivedMessage> messages)
    {
        var now = DateTimeOffset.UtcNow;
        foreach (var m in messages)
            _held[m.SequenceNumber] = new HeldMessage(m, m.LockedUntil - now);
    }

    private static Task AbandonAllAsync(ServiceBusReceiver receiver, IReadOnlyList<ServiceBusReceivedMessage> messages, ILogger logger) =>
        Parallel.ForEachAsync(messages, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (m, _) =>
        {
            try { await receiver.AbandonMessageAsync(m); }
            catch (Exception ex) { logger.LogWarning(ex, "Could not abandon message #{SeqNum}", m.SequenceNumber); }
        });

    public bool IsHeld(long sequenceNumber) => _held.ContainsKey(sequenceNumber);

    public Task CompleteAsync(MessageInfo message, CancellationToken ct = default) =>
        SettleAsync(message, m => _receiver.CompleteMessageAsync(m, ct));

    public Task AbandonAsync(MessageInfo message, CancellationToken ct = default) =>
        SettleAsync(message, m => _receiver.AbandonMessageAsync(m, cancellationToken: ct));

    public Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default) =>
        SettleAsync(message, m => _receiver.DeadLetterMessageAsync(m, reason, cancellationToken: ct));

    private async Task SettleAsync(MessageInfo message, Func<ServiceBusReceivedMessage, Task> settle)
    {
        if (!_held.TryGetValue(message.SequenceNumber, out var held))
            throw new InvalidOperationException($"Message #{message.SequenceNumber} is no longer locked");
        try
        {
            await settle(held.Message);
        }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            _held.TryRemove(message.SequenceNumber, out _);
            throw new MessageLockLostException(message.SequenceNumber, ex);
        }
        _held.TryRemove(message.SequenceNumber, out _);
    }

    private async Task RenewLoopAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try { await Task.Delay(TimeSpan.FromSeconds(1), ct); }
            catch (OperationCanceledException) { return; }

            var now = DateTimeOffset.UtcNow;
            var due = _held.Values.Where(h => h.Message.LockedUntil - now < h.LockDuration / 2).ToList();
            try
            {
                await Parallel.ForEachAsync(due, new ParallelOptions { MaxDegreeOfParallelism = 16, CancellationToken = ct }, RenewAsync);
            }
            catch (OperationCanceledException) { return; }
        }
    }

    private async ValueTask RenewAsync(HeldMessage h, CancellationToken token)
    {
        try
        {
            await _receiver.RenewMessageLockAsync(h.Message, token);
        }
        catch (OperationCanceledException) { }
        catch (ServiceBusException ex) when (ex.Reason == ServiceBusFailureReason.MessageLockLost)
        {
            if (!_held.TryRemove(h.Message.SequenceNumber, out _)) return; // settled while renewing
            _logger.LogWarning("Lost lock on message #{SeqNum}", h.Message.SequenceNumber);
            _lockLost.Writer.TryWrite(h.Message.SequenceNumber);
        }
        catch (Exception ex) when (_held.ContainsKey(h.Message.SequenceNumber))
        {
            _logger.LogWarning(ex, "Lock renewal failed for message #{SeqNum}; retrying", h.Message.SequenceNumber);
        }
        catch (Exception) { /* settled while renewing */ }
    }

    public async ValueTask DisposeAsync()
    {
        if (_cts.IsCancellationRequested) return;
        await _cts.CancelAsync();
        await _scan;
        await _renewLoop;
        _lockLost.Writer.TryComplete();
        var remaining = _held.Values.ToList();
        _held.Clear();
        if (remaining.Count > 0)
        {
            _logger.LogInformation("Abandoning {Count} still-locked messages", remaining.Count);
            await AbandonAllAsync(_receiver, [.. remaining.Select(h => h.Message)], _logger);
        }
        await _receiver.DisposeAsync();
    }
}
