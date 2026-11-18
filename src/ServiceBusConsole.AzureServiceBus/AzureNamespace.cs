using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>A namespace in Azure: what every tier has, metrics and deletion included.</summary>
internal abstract class AzureNamespace(BrokerInfo info, ServiceBusClient bus, AzureServices services)
    : IBroker, IHasPeek, IHasSeek, IHasDeadLetters, IHasMetrics, IHasDeletion
{
    public BrokerInfo Info => info;
    public IQueueService Queues => services.Queues;
    public IMessageService Messages => services.Messages;
    public IPeekService Peek => services.Messages;
    public ISeekService Seek => services.Messages;
    public IDeadLetterService DeadLetters => services.Messages;
    public IMetricsService Metrics => services.Metrics;
    public IDeletionService Deletion => services.Deletion;

    public ValueTask DisposeAsync() => bus.DisposeAsync();
}
