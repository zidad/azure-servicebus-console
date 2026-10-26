namespace ServiceBusConsole.UI;

/// <summary>A top-level list in a namespace, reachable from the header's section dropdown.</summary>
public sealed record Section(string Name, string Icon, string Route)
{
    public static Section Queues { get; } = new("Queues", Icons.Queue, "/queues");
    public static Section Topics { get; } = new("Topics", Icons.Topic, "/topics");
    public static Section AllSubscriptions { get; } = new("All Subscriptions", Icons.Subscription, "/subscriptions");

    public static IReadOnlyList<Section> All { get; } = [Queues, Topics, AllSubscriptions];

    public string Label => $"{Icon} {Name}";
}
