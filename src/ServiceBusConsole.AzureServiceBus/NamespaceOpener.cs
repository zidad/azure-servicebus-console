using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Opens a namespace as the emulator or by its Azure tier, with services wrapped by the registered decorators.</summary>
internal sealed class NamespaceOpener(
    AzureCliCredential credential, ArmClient armClient, ILoggerFactory loggers, IEnumerable<INamespaceDecorator> decorators) : INamespaceOpener
{
    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    /// <summary>
    /// By <see cref="NamespaceInfo.Sku"/>. An unknown one (cached before it was recorded) counts as Standard until the live
    /// listing replaces the namespace.
    /// </summary>
    public static NamespaceTier TierOf(NamespaceInfo info) => info switch
    {
        _ when info.FullyQualifiedNamespace == NamespaceInfo.Emulator.FullyQualifiedNamespace => NamespaceTier.Emulator,
        { Sku: { } sku } when sku.Equals("Basic", StringComparison.OrdinalIgnoreCase) => NamespaceTier.Basic,
        _ => NamespaceTier.Standard,
    };

    public IServiceBusNamespace Open(NamespaceInfo info)
    {
        var tier = TierOf(info);
        loggers.CreateLogger<NamespaceOpener>().LogInformation("Opening namespace {Namespace} ({Tier})", info.FullyQualifiedNamespace, tier);
        return tier == NamespaceTier.Emulator ? OpenEmulator() : OpenAzure(info, tier);
    }

    private EmulatorNamespace OpenEmulator()
    {
        var bus = new ServiceBusClient(EmulatorConnectionString);
        var ns = NamespaceInfo.Emulator;
        return new EmulatorNamespace(bus,
            Decorate(ns, new EmulatorQueueService(bus, loggers.CreateLogger<EmulatorQueueService>())),
            Decorate(ns, new EmulatorTopicService(bus, loggers.CreateLogger<EmulatorTopicService>())),
            new MessageService(bus, loggers));
    }

    private AzureNamespace OpenAzure(NamespaceInfo info, NamespaceTier tier)
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

    private IQueueService Decorate(NamespaceInfo ns, IQueueService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private ITopicService Decorate(NamespaceInfo ns, ITopicService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private IMetricsService Decorate(NamespaceInfo ns, IMetricsService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
    private IDeletionService Decorate(NamespaceInfo ns, IDeletionService service) => decorators.Aggregate(service, (s, d) => d.Decorate(ns, s));
}
