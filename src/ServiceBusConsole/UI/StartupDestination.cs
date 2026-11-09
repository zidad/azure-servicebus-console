namespace ServiceBusConsole.UI;

/// <summary>Where <c>--open</c> leads in the opened namespace: an entity's page or one of its lists.</summary>
public abstract record StartupDestination
{
    /// <summary>A queue or subscription opens on its dead-letter tab unless <paramref name="Active"/>.</summary>
    public sealed record Entity(JumpTarget Target, bool Active) : StartupDestination
    {
        public override T Match<T>(Func<Entity, T> entity, Func<List, T> list) => entity(this);
    }

    public sealed record List(Section Section) : StartupDestination
    {
        public override T Match<T>(Func<Entity, T> entity, Func<List, T> list) => list(this);
    }

    public abstract T Match<T>(Func<Entity, T> entity, Func<List, T> list);

    /// <summary>
    /// <paramref name="path"/> names a queue, topic or <c>topic/subscription</c> exactly (ignoring case), a queue or subscription
    /// optionally followed by <c>/dlq</c> or <c>/active</c>; else one of <paramref name="sections"/> by its route
    /// (queues, topics, subscriptions). Null when nothing matches.
    /// </summary>
    public static StartupDestination? Find(string path, IEnumerable<JumpTarget> targets, IReadOnlyList<Section> sections)
    {
        var all = targets.ToList();
        var trimmed = path.Trim().Trim('/');
        if (Named(all, trimmed) is { } whole) return new Entity(whole, Active: false);

        var slash = trimmed.LastIndexOf('/');
        bool? active = slash < 0 ? null : trimmed[(slash + 1)..].ToLowerInvariant() switch { "dlq" => false, "active" => true, _ => null };
        if (active is { } tab && Named(all, trimmed[..slash]) is { } target and not JumpTarget.Topic) return new Entity(target, tab);

        return sections.FirstOrDefault(s => string.Equals(s.Route.TrimStart('/'), trimmed, StringComparison.OrdinalIgnoreCase)) is { } section
            ? new List(section)
            : null;
    }

    private static JumpTarget? Named(IEnumerable<JumpTarget> targets, string name) =>
        targets.FirstOrDefault(t => string.Equals(t.Name, name, StringComparison.OrdinalIgnoreCase));
}
