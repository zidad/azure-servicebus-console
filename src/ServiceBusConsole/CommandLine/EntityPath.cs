namespace ServiceBusConsole.CommandLine;

/// <summary>
/// A queue or <c>topic/subscription</c> as a command names it; the Azure forms <c>topic/Subscriptions/sub</c> and a
/// trailing <c>/$DeadLetterQueue</c> work too. Queue and topic names may contain slashes, so a path is matched against the
/// namespace's queues, then its topics and their subscriptions.
/// </summary>
public static class EntityPath
{
    private const string DeadLetterSuffix = "/$DeadLetterQueue";
    private const string SubscriptionsSegment = "/Subscriptions/";

    public static async Task<MessageSource> ResolveAsync(IServiceBusNamespace ns, string path, bool dlq, CancellationToken ct)
    {
        var name = path.Trim().Trim('/');
        if (name.EndsWith(DeadLetterSuffix, StringComparison.OrdinalIgnoreCase))
        {
            name = name[..^DeadLetterSuffix.Length];
            dlq = true;
        }
        name = name.Replace(SubscriptionsSegment, "/", StringComparison.OrdinalIgnoreCase);

        var queue = await ns.Queues.GetQueuesAsync(ct).FirstOrDefaultAsync(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase), ct);
        if (queue is not null) return new MessageSource.Queue(queue.Name, dlq);

        if (ns is not IHasTopics { Topics: var topics }) throw NotFound(name);
        // Topic names may contain slashes (MassTransit's do), so try every topic the path starts with, longest first.
        var candidates = await topics.GetTopicsAsync(ct)
            .Where(t => name.Length > t.Name.Length + 1 && name.StartsWith(t.Name + "/", StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(t => t.Name.Length)
            .ToListAsync(ct);
        foreach (var topic in candidates)
        {
            var subscription = name[(topic.Name.Length + 1)..];
            var found = await topics.GetSubscriptionsAsync(topic.Name, ct)
                .FirstOrDefaultAsync(s => string.Equals(s.Name, subscription, StringComparison.OrdinalIgnoreCase), ct);
            if (found is not null) return new MessageSource.Subscription(topic.Name, found.Name, dlq);
        }
        throw NotFound(name);
    }

    private static CommandFailure NotFound(string name) => new(ExitCodes.NotFound, CommandLineStrings.FormatEntityPath_NotFound(name));

    /// <summary>A queue or topic to send to.</summary>
    public static async Task<string> ResolveSendTargetAsync(IServiceBusNamespace ns, string path, CancellationToken ct)
    {
        var name = path.Trim().Trim('/');
        var queue = await ns.Queues.GetQueuesAsync(ct).FirstOrDefaultAsync(q => string.Equals(q.Name, name, StringComparison.OrdinalIgnoreCase), ct);
        if (queue is not null) return queue.Name;
        var topic = ns is IHasTopics { Topics: var topics }
            ? await topics.GetTopicsAsync(ct).FirstOrDefaultAsync(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase), ct)
            : null;
        return topic?.Name ?? throw NotFound(name);
    }
}
