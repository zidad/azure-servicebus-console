namespace ServiceBusConsole.UI;

/// <summary>A top-level list in a namespace: one of the list pages' tabs (<see cref="Components.SectionTabs"/>).</summary>
public sealed record Section(string Name, string Icon, string Route)
{
    public static Section Queues { get; } = new(Strings.Queues, Icons.Queue, "/queues");
    public static Section Topics { get; } = new(Strings.Topics, Icons.Topic, "/topics");
    public static Section AllSubscriptions { get; } = new(Strings.AllSubscriptions, Icons.Subscription, "/subscriptions");

    public static IReadOnlyList<Section> All { get; } = [Queues, Topics, AllSubscriptions];

    /// <summary>The sections <paramref name="ns"/> has: topic ones only with topics.</summary>
    public static IReadOnlyList<Section> Of(IServiceBusNamespace? ns) => ns is IHasTopics ? [Queues, Topics, AllSubscriptions] : [Queues];

    public string Label => $"{Icon} {Name}";
}
