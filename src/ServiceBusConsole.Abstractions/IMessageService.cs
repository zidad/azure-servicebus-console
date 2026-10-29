namespace ServiceBusConsole.Abstractions;

/// <summary>Message operations; results arrive as streams (pages, running totals) that do the work as they are read.</summary>
public interface IMessageService
{
    /// <summary>Peeks up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default);

    /// <summary>Receives and deletes up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default);

    /// <summary>Starts receiving up to <paramref name="count"/> messages in PeekLock mode; each arrives on <see cref="ILockedMessages.Held"/>.</summary>
    ILockedMessages StartReceiving(MessageSource source, int count, CancellationToken ct = default);

    /// <summary>
    /// Starts locking these messages by receiving from the front; each arrives on <see cref="ILockedMessages.Held"/>.
    /// Messages passed on the way are released (delivery count + 1).
    /// </summary>
    ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, CancellationToken ct = default);

    /// <summary>
    /// Sends copies of messages this service returned, in order and in as few batches as fit;
    /// yields the running total after each batch.
    /// </summary>
    IAsyncEnumerable<int> SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, CancellationToken ct = default);

    /// <summary>Deletes every message; yields the running total.</summary>
    IAsyncEnumerable<int> PurgeAsync(MessageSource source, CancellationToken ct = default);
}
