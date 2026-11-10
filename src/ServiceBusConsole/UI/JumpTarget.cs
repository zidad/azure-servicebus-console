namespace ServiceBusConsole.UI;

/// <summary>Something the jump dialog (<c>:</c>) can open: an entity of the current namespace or, from the cache, of another one, or another namespace.</summary>
public abstract record JumpTarget
{
    public sealed record Queue(QueueInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Namespace, T> ns) => queue(this);
    }

    public sealed record Topic(TopicInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Namespace, T> ns) => topic(this);
    }

    public sealed record Subscription(SubscriptionInfo Info) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Namespace, T> ns) => subscription(this);
    }

    /// <summary>Another namespace; opening it switches the connection.</summary>
    public sealed record Namespace(JumpNamespace Place) : JumpTarget
    {
        public override T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Namespace, T> ns) => ns(this);
    }

    /// <summary>Exhaustive: one function per kind of target.</summary>
    public abstract T Match<T>(Func<Queue, T> queue, Func<Topic, T> topic, Func<Subscription, T> subscription, Func<Namespace, T> ns);

    /// <summary>The other namespace an entity is in (found with <c>^a</c>); null in the current one.</summary>
    public JumpNamespace? Elsewhere { get; init; }

    /// <summary>Where opening it goes: another namespace's own, or an entity's elsewhere; null in the current one.</summary>
    public JumpNamespace? OtherNamespace => this is Namespace ns ? ns.Place : Elsewhere;

    /// <summary>What a query matches: the name, "topic/subscription" for a subscription.</summary>
    public string Name => Match(q => q.Info.Name, t => t.Info.Name, s => $"{s.Info.TopicName}/{s.Info.Name}", n => n.Place.Info.Name);

    public string Label => Match(q => $"{Icons.Queue} {q.Info.Name}", t => $"{Icons.Topic} {t.Info.Name}", s => $"{Icons.Subscription} {s.Info.TopicName} › {s.Info.Name}",
        n => $"{Icons.Namespace} {n.Place.Info.Name}");

    public string Kind => Match(_ => UIStrings.JumpTarget_Queue, _ => UIStrings.JumpTarget_Topic, _ => UIStrings.JumpTarget_Subscription, _ => UIStrings.JumpTarget_Namespace);

    public JumpKind Group => Match(_ => JumpKind.Queue, _ => JumpKind.Topic, _ => JumpKind.Subscription, _ => JumpKind.Namespace);

    public long DeadLetters => Match(q => q.Info.DeadLetterMessageCount, _ => 0, s => s.Info.DeadLetterMessageCount, _ => 0);
}
