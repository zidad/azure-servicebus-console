using System.Xml;

namespace ServiceBusConsole.Services.Caching;

/// <summary>Where a namespace's listings are cached, so deleting an entity can drop it from them.</summary>
public static class CacheKeys
{
    public static string Queues(NamespaceInfo ns) => $"{ns.FullyQualifiedNamespace}/queues.json";
    public static string Topics(NamespaceInfo ns) => $"{ns.FullyQualifiedNamespace}/topics.json";
    public static string AllSubscriptions(NamespaceInfo ns) => $"{ns.FullyQualifiedNamespace}/all-subscriptions.json";
    public static string Subscriptions(NamespaceInfo ns, string topicName) => $"{ns.FullyQualifiedNamespace}/topics/{topicName}/subscriptions.json";

    // Spans and intervals in ISO 8601, as Azure Monitor names its grains: P1D, PT30M.
    public static string Metrics(NamespaceInfo ns, MetricsScope scope, MetricsWindow window) =>
        $"{ns.FullyQualifiedNamespace}/metrics/{scope.Match(_ => "namespace", e => $"entities/{e.Name}")}/"
        + $"{XmlConvert.ToString(window.To - window.From)}-{XmlConvert.ToString(window.Interval)}.json";
}
