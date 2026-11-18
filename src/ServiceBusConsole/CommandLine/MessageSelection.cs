namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Which messages a command acts on, found by peeking the first <see cref="Limit"/> messages: by message ID (every
/// message with that ID), by sequence number, or all of them.
/// </summary>
public sealed record MessageSelection
{
    public IReadOnlyList<string> MessageIds { get; init; } = [];
    public IReadOnlyList<long> SequenceNumbers { get; init; } = [];
    public bool All { get; init; }
    public int Limit { get; init; } = DefaultLimit;

    public const int DefaultLimit = 1000;

    public bool IsEmpty => !All && MessageIds.Count == 0 && SequenceNumbers.Count == 0;

    public async Task<Found> FindAsync(IPeekService peek, MessageSource source, CancellationToken ct)
    {
        var ids = MessageIds.ToHashSet(StringComparer.Ordinal);
        var keys = SequenceNumbers.Select(MessageKey.From).ToHashSet();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        var targets = new List<MessageInfo>();
        await foreach (var page in peek.PeekAsync(source, Limit, ct))
        {
            foreach (var m in page.Where(m => All || ids.Contains(m.MessageId) || keys.Contains(m.Key)))
            {
                targets.Add(m);
                seenIds.Add(m.MessageId);
            }
            // Sequence numbers are unique; an ID may come back again (a retried copy), so ID lookups read the whole window.
            if (!All && ids.Count == 0 && keys.All(k => targets.Any(t => t.Key == k))) break;
        }
        return new(
            targets,
            [.. MessageIds.Distinct().Where(id => !seenIds.Contains(id))],
            [.. SequenceNumbers.Distinct().Where(s => targets.All(t => t.Key != MessageKey.From(s)))]);
    }

    /// <param name="Targets">The matching messages, in queue order.</param>
    /// <param name="MissingIds">Requested IDs not among the peeked messages.</param>
    /// <param name="MissingSequenceNumbers">Requested sequence numbers not among them.</param>
    public sealed record Found(IReadOnlyList<MessageInfo> Targets, IReadOnlyList<string> MissingIds, IReadOnlyList<long> MissingSequenceNumbers)
    {
        public bool AnyMissing => MissingIds.Count > 0 || MissingSequenceNumbers.Count > 0;
    }
}
