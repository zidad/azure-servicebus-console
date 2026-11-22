using System.Xml;

namespace ServiceBusConsole.Core.Services.Caching;

/// <summary>Where a namespace's listings are cached, so deleting an entity can drop it from them.</summary>
public static class CacheKeys
{
    public static string Brokers(string subscriptionId) => $"{subscriptionId}/namespaces.json";
    public static string Queues(BrokerInfo ns) => $"{ns.Address}/queues.json";
    public static string Topics(BrokerInfo ns) => $"{ns.Address}/topics.json";
    public static string AllSubscriptions(BrokerInfo ns) => $"{ns.Address}/all-subscriptions.json";
    public static string Subscriptions(BrokerInfo ns, string topicName) => $"{ns.Address}/topics/{topicName}/subscriptions.json";

    public static string IncomingByEntity(BrokerInfo ns, MetricsWindow window) =>
        $"{ns.Address}/metrics/incoming-by-entity/{XmlConvert.ToString(window.To - window.From)}-{XmlConvert.ToString(window.Interval)}.json";

    // Spans and intervals in ISO 8601, as Azure Monitor names its grains: P1D, PT30M.
    public static string Metrics(BrokerInfo ns, MetricsScope scope, MetricsWindow window) =>
        $"{ns.Address}/metrics/{scope.Match(_ => "namespace", e => $"entities/{e.Name}")}/"
        + $"{XmlConvert.ToString(window.To - window.From)}-{XmlConvert.ToString(window.Interval)}.json";
}
