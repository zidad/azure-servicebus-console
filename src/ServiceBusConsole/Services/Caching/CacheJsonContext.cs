using System.Text.Json.Serialization;

namespace ServiceBusConsole.Services.Caching;

/// <summary>Every type <see cref="FileCache"/> stores: source-generated, so caching needs no reflection (Native AOT).</summary>
[JsonSerializable(typeof(List<AzureSubscription>))]
[JsonSerializable(typeof(List<NamespaceInfo>))]
[JsonSerializable(typeof(List<QueueInfo>))]
[JsonSerializable(typeof(List<TopicInfo>))]
[JsonSerializable(typeof(List<SubscriptionInfo>))]
[JsonSerializable(typeof(List<MessageMetrics>))]
[JsonSerializable(typeof(List<EntityTraffic>))]
[JsonSerializable(typeof(List<bool>))]
[JsonSerializable(typeof(List<TimeSpan>))]
[JsonSerializable(typeof(List<RecentNamespace>))]
internal sealed partial class CacheJsonContext : JsonSerializerContext;
