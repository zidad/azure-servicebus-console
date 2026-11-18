namespace ServiceBusConsole.Services.Caching;

/// <summary>Caches each namespace's listings and metrics: cached data first, then live.</summary>
public sealed class CachingNamespaceDecorator(FileCache cache) : IBrokerDecorator
{
    public IQueueService Decorate(BrokerInfo ns, IQueueService queues) => new CachingQueueService(queues, ns, cache);
    public ITopicService Decorate(BrokerInfo ns, ITopicService topics) => new CachingTopicService(topics, ns, cache);
    public IMetricsService Decorate(BrokerInfo ns, IMetricsService metrics) => new CachingMetricsService(metrics, ns, cache);
    public IDeletionService Decorate(BrokerInfo ns, IDeletionService deletion) => new CachingDeletionService(deletion, ns, cache);
}
