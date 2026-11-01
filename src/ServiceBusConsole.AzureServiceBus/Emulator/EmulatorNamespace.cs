using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

/// <summary>The local emulator: topics, but no management API (no deletion), no metrics and no transactions.</summary>
internal sealed class EmulatorNamespace(ServiceBusClient bus, IQueueService queues, ITopicService topics, IMessageService messages)
    : IServiceBusNamespace, IHasTopics
{
    public NamespaceInfo Info => NamespaceInfo.Emulator;
    public IQueueService Queues => queues;
    public ITopicService Topics => topics;
    public IMessageService Messages => messages;

    public ValueTask DisposeAsync() => bus.DisposeAsync();
}
