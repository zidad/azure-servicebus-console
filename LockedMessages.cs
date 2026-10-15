using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

/// <summary>
/// Messages received in PeekLock mode. Locks belong to the receiver link, so the receiver stays
/// open until disposal, locks are renewed in the background, and whatever is still held on
/// disposal is abandoned so it becomes visible again right away instead of after lock expiry.
/// </summary>
public sealed class LockedMessages : IAsyncDisposable
{
    private readonly ServiceBusReceiver _receiver;
    private readonly ILogger _logger;
    private readonly ConcurrentDictionary<long, Held> _held = new();
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _renewLoop;

    private sealed record Held(ServiceBusReceivedMessage Message, TimeSpan LockDuration);

    public IReadOnlyList<ServiceBusReceivedMessage> Messages { get; }

    /// <summary>Raised from a background thread with the sequence number of a message whose lock was lost.</summary>
    public event Action<long>? LockLost;

    public LockedMessages(ServiceBusReceiver receiver, IReadOnlyList<ServiceBusReceivedMessage> messages, ILogger logger)
    {
        _receiver = receiver;
        _logger = logger;
        Messages = messages;
        var now = DateTimeOffset.UtcNow;
        foreach (var m in messages)
            _held[m.SequenceNumber] = new Held(m, m.LockedUntil - now);
        _renewLoop = RenewLoopAsync(_cts.Token);
    }

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
            throw;
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

    private async ValueTask RenewAsync(Held h, CancellationToken token)
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
            LockLost?.Invoke(h.Message.SequenceNumber);
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
        await _renewLoop;
        var remaining = _held.Values.ToList();
        _held.Clear();
        if (remaining.Count > 0)
        {
            _logger.LogInformation("Abandoning {Count} still-locked messages", remaining.Count);
            await Parallel.ForEachAsync(remaining, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (h, _) =>
            {
                try { await _receiver.AbandonMessageAsync(h.Message); }
                catch (Exception ex) { _logger.LogWarning(ex, "Could not abandon message #{SeqNum}", h.Message.SequenceNumber); }
            });
        }
        await _receiver.DisposeAsync();
    }
}
