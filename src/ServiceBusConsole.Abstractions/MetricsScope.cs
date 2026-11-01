namespace ServiceBusConsole.Abstractions;

/// <summary>What metrics describe: the whole namespace, or one queue or topic (Azure Monitor has no per-subscription split).</summary>
public abstract record MetricsScope
{
    public sealed record Namespace : MetricsScope
    {
        public override T Match<T>(Func<Namespace, T> ns, Func<Entity, T> entity) => ns(this);
    }

    public sealed record Entity(string Name) : MetricsScope
    {
        public override T Match<T>(Func<Namespace, T> ns, Func<Entity, T> entity) => entity(this);
    }

    /// <summary>Exhaustive: one function per kind of scope.</summary>
    public abstract T Match<T>(Func<Namespace, T> ns, Func<Entity, T> entity);
}
