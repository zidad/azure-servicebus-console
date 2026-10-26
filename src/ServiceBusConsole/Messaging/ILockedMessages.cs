namespace ServiceBusConsole.Messaging;

/// <summary>Messages held in PeekLock mode, settled one by one; disposal releases what is left.</summary>
public interface ILockedMessages : IAsyncDisposable
{
    /// <summary>Raised from a background thread with the sequence number of a message whose lock was lost.</summary>
    event Action<long>? LockLost;

    bool IsHeld(long sequenceNumber);
    Task CompleteAsync(MessageInfo message, CancellationToken ct = default);
    Task AbandonAsync(MessageInfo message, CancellationToken ct = default);
    Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default);
}
