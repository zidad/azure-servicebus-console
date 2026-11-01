namespace ServiceBusConsole.Abstractions;

/// <summary>
/// Wraps the services of every namespace an <see cref="INamespaceOpener"/> opens, e.g. to cache listings. Features are
/// interfaces of the namespace, not of its services, so wrapping a service keeps them.
/// </summary>
public interface INamespaceDecorator
{
    IQueueService Decorate(NamespaceInfo ns, IQueueService queues);
    ITopicService Decorate(NamespaceInfo ns, ITopicService topics);
    IMetricsService Decorate(NamespaceInfo ns, IMetricsService metrics);
    IDeletionService Decorate(NamespaceInfo ns, IDeletionService deletion);
}
