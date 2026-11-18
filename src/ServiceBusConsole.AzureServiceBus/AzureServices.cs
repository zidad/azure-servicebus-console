namespace ServiceBusConsole.AzureServiceBus;

/// <summary>The services every Azure tier has.</summary>
internal sealed record AzureServices(IQueueService Queues, MessageService Messages, IMetricsService Metrics, IDeletionService Deletion);
