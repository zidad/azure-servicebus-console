namespace ServiceBusConsole.Services;

public interface IMessageService
{
    /// <summary>Peeks up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, CancellationToken ct = default);

    /// <summary>Receives and deletes up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, CancellationToken ct = default);

    /// <summary>
    /// Starts receiving up to <paramref name="count"/> messages in PeekLock mode; each is written to
    /// <paramref name="received"/> once held. See <see cref="LockedMessages.StartReceive"/>.
    /// </summary>
    ILockedMessages StartReceiving(MessageSource source, int count, System.Threading.Channels.ChannelWriter<Azure.Messaging.ServiceBus.ServiceBusReceivedMessage> received, CancellationToken ct = default);

    /// <summary>
    /// Starts locking these messages by receiving from the front; each is written to <paramref name="found"/>
    /// once held. See <see cref="LockedMessages.StartScan"/>.
    /// </summary>
    ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, System.Threading.Channels.ChannelWriter<Azure.Messaging.ServiceBus.ServiceBusReceivedMessage> found, CancellationToken ct = default);
    /// <summary>Sends copies in order, in as few batches as fit; <paramref name="onSent"/> gets the running total after each batch.</summary>
    Task SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, Action<int> onSent, CancellationToken ct = default);
    /// <summary>Deletes every message; <paramref name="onPurged"/> gets the running total.</summary>
    Task<int> PurgeMessagesAsync(MessageSource source, Action<int> onPurged, CancellationToken ct = default);
}
