using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>The Standard and Premium tiers: topics and transactions too.</summary>
internal sealed class StandardNamespace(NamespaceInfo info, ServiceBusClient bus, AzureServices services, ITopicService topics, ITransactionService transactions)
    : AzureNamespace(info, bus, services), IHasTopics, IHasTransactions
{
    public ITopicService Topics => topics;
    public ITransactionService Transactions => transactions;
}
