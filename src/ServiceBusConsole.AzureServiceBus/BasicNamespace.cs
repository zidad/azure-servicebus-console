using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>The Basic tier: queues only, no topics and no transactions.</summary>
internal sealed class BasicNamespace(NamespaceInfo info, ServiceBusClient bus, AzureServices services)
    : AzureNamespace(info, bus, services);
