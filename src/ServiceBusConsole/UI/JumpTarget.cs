namespace ServiceBusConsole.UI;

/// <summary>Something the jump dialog (<c>:</c>) can open in the current namespace.</summary>
public abstract record JumpTarget
{
    public sealed record Queue(QueueInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription) => queue(this);
    }

    public sealed record Topic(TopicInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription) => topic(this);
    }

    public sealed record Subscription(SubscriptionInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription) => subscription(this);
    }

    /// <summary>Exhaustive: one function per kind of target.</summary>
    public abstract T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription);

    /// <summary>What a query matches: the name, "topic/subscription" for a subscription.</summary>
    public string Name => Match(q => q.Info.Name, t => t.Info.Name, s => $"{s.Info.TopicName}/{s.Info.Name}");

    public string Label => Match(q => $"{Icons.Queue} {q.Info.Name}", t => $"{Icons.Topic} {t.Info.Name}", s => $"{Icons.Subscription} {s.Info.TopicName} › {s.Info.Name}");

    public string Kind => Match(_ => "queue", _ => "topic", _ => "subscription");

    public long DeadLetters => Match(q => q.Info.DeadLetterMessageCount, _ => 0, s => s.Info.DeadLetterMessageCount);
}
