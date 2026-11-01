using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>The Standard and Premium tiers: topics too.</summary>
internal sealed class StandardNamespace(NamespaceInfo info, ServiceBusClient bus, AzureServices services, ITopicService topics)
    : AzureNamespace(info, bus, services), IHasTopics
{
    public ITopicService Topics => topics;
}
