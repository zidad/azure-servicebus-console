using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Opens a namespace as the emulator or by its Azure tier, with services wrapped by the registered decorators.</summary>
internal sealed class NamespaceOpener(
    AzureCliCredential credential, ArmClient armClient, ILoggerFactory loggers, IEnumerable<IBrokerDecorator> decorators,
    EmulatorSettings emulator) : IBrokerOpener
{
    /// <summary>
    /// By <see cref="BrokerInfo.Sku"/>. An unknown one (cached before it was recorded) counts as Standard until the live
    /// listing replaces the namespace.
    /// </summary>
    public static NamespaceTier TierOf(BrokerInfo info) => info switch
    {
        _ when info.FullyQualifiedNamespace == BrokerInfo.Emulator.FullyQualifiedNamespace => NamespaceTier.Emulator,
        { Sku: { } sku } when sku.Equals("Basic", StringComparison.OrdinalIgnoreCase) => NamespaceTier.Basic,
        _ => NamespaceTier.Standard,
    };

    public IBroker Open(BrokerInfo info)
    {
        var tier = TierOf(info);
        loggers.CreateLogger<NamespaceOpener>().LogInformation("Opening namespace {Namespace} ({Tier})", info.FullyQualifiedNamespace, tier);
        return tier == NamespaceTier.Emulator ? OpenEmulator() : OpenAzure(info, tier);
    }

    private EmulatorNamespace OpenEmulator()
    {
        var bus = new ServiceBusClient(emulator.ConnectionString);
        var ns = BrokerInfo.Emulator;
        return new EmulatorNamespace(bus,
            Decorate(ns, new EmulatorQueueService(bus, emulator, loggers.CreateLogger<EmulatorQueueService>())),
            Decorate(ns, new EmulatorTopicService(bus, emulator, loggers.CreateLogger<EmulatorTopicService>())),
            new MessageService(bus, loggers));
    }

    private AzureNamespace OpenAzure(BrokerInfo info, NamespaceTier tier)
    {
        var bus = new ServiceBusClient($"sb://{info.FullyQualifiedNamespace}/", credential);
        var admin = new ServiceBusAdministrationClient($"https://{info.FullyQualifiedNamespace}", credential);
        var services = new AzureServices(
            Decorate(info, new QueueService(admin, loggers.CreateLogger<QueueService>())),
            new MessageService(bus, loggers),
            Decorate(info, new MetricsService(info, armClient, loggers.CreateLogger<MetricsService>())),
            Decorate(info, new DeletionService(admin, loggers.CreateLogger<DeletionService>())));
        return tier == NamespaceTier.Basic
            ? new BasicNamespace(info, bus, services)
            : new StandardNamespace(info, bus, services,
                Decorate(info, new TopicService(admin, loggers.CreateLogger<TopicService>())),
                new TransactionService(bus, loggers.CreateLogger<TransactionService>()));
    }

    private IQueueService Decorate(BrokerInfo ns, IQueueService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private ITopicService Decorate(BrokerInfo ns, ITopicService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private IMetricsService Decorate(BrokerInfo ns, IMetricsService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private IDeletionService Decorate(BrokerInfo ns, IDeletionService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
}
