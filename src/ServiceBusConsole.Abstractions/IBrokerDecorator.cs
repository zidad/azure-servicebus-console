namespace ServiceBusConsole.Abstractions;

/// <summary>
/// Wraps the services of every namespace an <see cref="IBrokerOpener"/> opens, e.g. to cache listings. Features are
/// interfaces of the namespace, not of its services, so wrapping a service keeps them.
/// </summary>
public interface IBrokerDecorator
{
    IQueueService Decorate(BrokerInfo ns, IQueueService queues);
    ITopicService Decorate(BrokerInfo ns, ITopicService topics);
    IMetricsService Decorate(BrokerInfo ns, IMetricsService metrics);
    IDeletionService Decorate(BrokerInfo ns, IDeletionService deletion);
}
