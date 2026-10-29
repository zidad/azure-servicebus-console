namespace ServiceBusConsole.Abstractions;

/// <summary>Where messages are read: a queue or a topic subscription, active or dead-letter.</summary>
public abstract record MessageSource(bool IsDlq)
{
    public sealed record Queue(string Name, bool IsDlq = false) : MessageSource(IsDlq)
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Subscription, T> subscription) => queue(this);
    }

    public sealed record Subscription(string Topic, string Name, bool IsDlq = false) : MessageSource(IsDlq)
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Subscription, T> subscription) => subscription(this);
    }

    /// <summary>Exhaustive: one function per kind of source.</summary>
    public abstract T Match<T>(Func<Queue, T> queue, Func<Subscription, T> subscription);

    /// <summary>Where copies go: the queue itself, or the topic of a subscription.</summary>
    public string SendTarget => Match(q => q.Name, s => s.Topic);

    public string DisplayName => Match(q => q.Name, s => $"{s.Topic}/{s.Name}") + (IsDlq ? " DLQ" : "");
}
