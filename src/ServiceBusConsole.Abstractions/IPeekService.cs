namespace ServiceBusConsole.Abstractions;

/// <summary>Reads messages from the front without settling them.</summary>
public interface IPeekService
{
    PeekKind Kind { get; }

    /// <summary>Peeks up to <paramref name="count"/> messages, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, CancellationToken ct = default);
}
