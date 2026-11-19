namespace ServiceBusConsole.Abstractions;

/// <summary>What only dead letters allow: <see cref="MessageSource.IsDlq"/> sources, wherever the transport keeps them.</summary>
public interface IDeadLetterService
{
    /// <summary>
    /// Starts locking these dead letters by receiving from the front; each arrives on <see cref="ILockedMessages.Held"/>.
    /// Messages passed on the way are released (delivery count + 1), which only dead letters can afford.
    /// </summary>
    ILockedMessages StartLocking(MessageSource deadLetters, IReadOnlyCollection<MessageKey> keys, CancellationToken ct = default);
}
