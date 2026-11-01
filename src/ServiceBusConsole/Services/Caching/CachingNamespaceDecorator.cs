namespace ServiceBusConsole.Services.Caching;

/// <summary>Caches each namespace's listings and metrics: cached data first, then live.</summary>
public sealed class CachingNamespaceDecorator(FileCache cache) : INamespaceDecorator
{
    public IQueueService Decorate(NamespaceInfo ns, IQueueService queues) => new CachingQueueService(queues, ns, cache);
    public ITopicService Decorate(NamespaceInfo ns, ITopicService topics) => new CachingTopicService(topics, ns, cache);
    public IMetricsService Decorate(NamespaceInfo ns, IMetricsService metrics) => new CachingMetricsService(metrics, ns, cache);
    public IDeletionService Decorate(NamespaceInfo ns, IDeletionService deletion) => new CachingDeletionService(deletion, ns, cache);
}
