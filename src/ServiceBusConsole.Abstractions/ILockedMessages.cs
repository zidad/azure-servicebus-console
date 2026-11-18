using System.Threading.Channels;

namespace ServiceBusConsole.Abstractions;

/// <summary>
/// Messages held in PeekLock mode, settled one by one; disposal releases what is left. Settling a message
/// whose lock was lost throws <see cref="MessageLockLostException"/>.
/// </summary>
public interface ILockedMessages : IAsyncDisposable
{
    /// <summary>Each message once held; completes when receiving ends, with the error if it failed.</summary>
    ChannelReader<MessageInfo> Held { get; }

    /// <summary>Held messages whose lock was lost; completes on disposal.</summary>
    ChannelReader<MessageKey> LockLost { get; }

    bool IsHeld(MessageKey key);
    Task CompleteAsync(MessageInfo message, CancellationToken ct = default);
    Task AbandonAsync(MessageInfo message, CancellationToken ct = default);
    Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default);
}
