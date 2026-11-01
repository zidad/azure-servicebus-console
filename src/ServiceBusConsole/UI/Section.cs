namespace ServiceBusConsole.UI;

/// <summary>A top-level list in a namespace, reachable from the header's section dropdown.</summary>
public sealed record Section(string Name, string Icon, string Route)
{
    public static Section Queues { get; } = new("Queues", Icons.Queue, "/queues");
    public static Section Topics { get; } = new("Topics", Icons.Topic, "/topics");
    public static Section AllSubscriptions { get; } = new("All Subscriptions", Icons.Subscription, "/subscriptions");

    /// <summary>The sections <paramref name="ns"/> has: topic ones only with topics.</summary>
    public static IReadOnlyList<Section> Of(IServiceBusNamespace? ns) => ns is IHasTopics ? [Queues, Topics, AllSubscriptions] : [Queues];

    public string Label => $"{Icon} {Name}";
}
