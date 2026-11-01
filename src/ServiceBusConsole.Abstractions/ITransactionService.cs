namespace ServiceBusConsole.Abstractions;

public interface ITransactionService
{
    /// <summary>Most messages one <see cref="MoveAsync"/> takes: a transaction sends at most 100.</summary>
    int MaxMessages { get; }

    /// <summary>
    /// Sends copies of <paramref name="messages"/> to the send target of <paramref name="source"/> in order and completes the
    /// originals, in one transaction: all of them move, or none do and <paramref name="locked"/> still holds them. Takes
    /// messages this namespace returned that <paramref name="locked"/> holds; a lost lock throws <see cref="MessageLockLostException"/>.
    /// </summary>
    Task MoveAsync(MessageSource source, ILockedMessages locked, IReadOnlyList<MessageInfo> messages, CancellationToken ct = default);
}
