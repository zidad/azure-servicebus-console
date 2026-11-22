namespace ServiceBusConsole.UI;

/// <summary>Something the jump dialog (<c>:</c>) can open: an entity of the current namespace or, from the cache, of another one, or another namespace.</summary>
public abstract record JumpTarget
{
    public sealed record Queue(QueueInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Broker, T> broker) => queue(this);
    }

    public sealed record Topic(TopicInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Broker, T> broker) => topic(this);
    }

    public sealed record Subscription(SubscriptionInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Broker, T> broker) => subscription(this);
    }

    /// <summary>Another namespace; opening it switches the connection.</summary>
    public sealed record Broker(JumpBroker Place) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Broker, T> broker) => broker(this);
    }

    /// <summary>Exhaustive: one function per kind of target.</summary>
    public abstract T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Broker, T> broker);

    /// <summary>The other namespace an entity is in (found with <c>^a</c>); null in the current one.</summary>
    public JumpBroker? Elsewhere { get; init; }

    /// <summary>Where opening it goes: another namespace's own, or an entity's elsewhere; null in the current one.</summary>
    public JumpBroker? OtherBroker => this is Broker broker ? broker.Place : Elsewhere;

    /// <summary>What a query matches: the name, "topic/subscription" for a subscription.</summary>
    public string Name => Match(q => q.Info.Name, t => t.Info.Name, s => $"{s.Info.TopicName}/{s.Info.Name}", n => n.Place.Info.Name);

    public string Label => Match(q => $"{Icons.Queue} {q.Info.Name}", t => $"{Icons.Topic} {t.Info.Name}", s => $"{Icons.Subscription} {s.Info.TopicName} › {s.Info.Name}",
        n => $"{Terms.Broker.Icon} {n.Place.Info.Name}");

    public string Kind => Match(_ => UIStrings.JumpTarget_Queue, _ => UIStrings.JumpTarget_Topic, _ => UIStrings.JumpTarget_Subscription, _ => Terms.Broker.OneInText);

    public JumpKind Group => Match(_ => JumpKind.Queue, _ => JumpKind.Topic, _ => JumpKind.Subscription, _ => JumpKind.Broker);

    public long DeadLetters => Match(q => q.Info.DeadLetterMessageCount, _ => 0, s => s.Info.DeadLetterMessageCount, _ => 0);
}
