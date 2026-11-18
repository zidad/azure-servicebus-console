namespace ServiceBusConsole.Abstractions;

/// <summary>Peeks from a position in the entity's own numbering, not only from the front.</summary>
public interface ISeekService
{
    /// <summary>Peeks up to <paramref name="count"/> messages from <paramref name="position"/> on, one page per round trip.</summary>
    IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekFromAsync(MessageSource source, long position, int count, CancellationToken ct = default);
}
