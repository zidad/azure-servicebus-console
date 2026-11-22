namespace ServiceBusConsole.Abstractions;

/// <summary>What metrics describe: the whole namespace, or one queue or topic (Azure Monitor has no per-subscription split).</summary>
public abstract record MetricsScope
{
    public sealed record Broker : MetricsScope
    {
        public override T Match<T>(Func<Broker, T> broker, Func<Entity, T> entity) => broker(this);
    }

    public sealed record Entity(string Name) : MetricsScope
    {
        public override T Match<T>(Func<Broker, T> broker, Func<Entity, T> entity) => entity(this);
    }

    /// <summary>Exhaustive: one function per kind of scope.</summary>
    public abstract T Match<T>(Func<Broker, T> broker, Func<Entity, T> entity);
}
