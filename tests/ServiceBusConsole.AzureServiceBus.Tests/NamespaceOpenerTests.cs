using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class NamespaceOpenerTests
{
    private static NamespaceOpener Opener(params INamespaceDecorator[] decorators) =>
        new(new AzureCliCredential(), new ArmClient(new AzureCliCredential()), NullLoggerFactory.Instance, decorators);

    private static NamespaceInfo Azure(string? sku) => new()
    {
        Name = "sb-orders", FullyQualifiedNamespace = "sb-orders.servicebus.windows.net", ResourceGroup = "rg", Location = "West Europe", Sku = sku,
    };

    private static string[] Features(IServiceBusNamespace ns) =>
    [
        .. new (bool Has, string Name)[]
            {
                (ns is IHasTopics, "topics"), (ns is IHasMetrics, "metrics"),
                (ns is IHasDeletion, "deletion"), (ns is IHasTransactions, "transactions"),
            }
            .Where(f => f.Has).Select(f => f.Name),
    ];

    [Test]
    public async Task Emulator_HasTopics_ButNoMetricsDeletionOrTransactions()
    {
        await using var ns = Opener().Open(NamespaceInfo.Emulator);

        ns.ShouldBeOfType<EmulatorNamespace>();
        Features(ns).ShouldBe(["topics"]);
    }

    [Test]
    [Arguments("Basic")]
    [Arguments("basic")]
    public async Task Basic_HasMetricsAndDeletion_ButNoTopicsOrTransactions(string sku)
    {
        await using var ns = Opener().Open(Azure(sku));

        ns.ShouldBeOfType<BasicNamespace>();
        Features(ns).ShouldBe(["metrics", "deletion"]);
    }

    [Test]
    [Arguments("Standard")]
    [Arguments("Premium")]
    [Arguments(null)]
    public async Task StandardPremiumAndUnknown_HaveEveryFeature(string? sku)
    {
        await using var ns = Opener().Open(Azure(sku));

        ns.ShouldBeOfType<StandardNamespace>();
        Features(ns).ShouldBe(["topics", "metrics", "deletion", "transactions"]);
        ns.Info.ShouldBe(Azure(sku));
    }

    [Test]
    public async Task Decorators_WrapTheServices_AndKeepTheFeatures()
    {
        await using var ns = Opener(new Tagging()).Open(Azure("Standard"));

        ns.Queues.ShouldBeOfType<Tagging.Queues>();
        ns.ShouldBeAssignableTo<IHasTopics>().Topics.ShouldBeOfType<Tagging.Topics>();
        ns.ShouldBeAssignableTo<IHasMetrics>().Metrics.ShouldBeOfType<Tagging.Metrics>();
        ns.ShouldBeAssignableTo<IHasDeletion>().Deletion.ShouldBeOfType<Tagging.Deletion>();
    }

    private sealed class Tagging : INamespaceDecorator
    {
        public sealed class Queues(IQueueService inner) : IQueueService
        {
            public IAsyncEnumerable<QueueInfo> GetQueuesAsync(CancellationToken ct = default) => inner.GetQueuesAsync(ct);
        }

        public sealed class Topics(ITopicService inner) : ITopicService
        {
            public IAsyncEnumerable<TopicInfo> GetTopicsAsync(CancellationToken ct = default) => inner.GetTopicsAsync(ct);
            public IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, CancellationToken ct = default) => inner.GetSubscriptionsAsync(topicName, ct);
            public IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync(CancellationToken ct = default) => inner.GetAllSubscriptionsAsync(ct);
        }

        public sealed class Metrics(IMetricsService inner) : IMetricsService
        {
            public IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, CancellationToken ct = default) =>
                inner.GetMetricsAsync(scope, window, ct);
        }

        public sealed class Deletion(IDeletionService inner) : IDeletionService
        {
            public Task DeleteQueueAsync(string queueName, CancellationToken ct = default) => inner.DeleteQueueAsync(queueName, ct);
            public Task DeleteTopicAsync(string topicName, CancellationToken ct = default) => inner.DeleteTopicAsync(topicName, ct);
            public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) =>
                inner.DeleteSubscriptionAsync(topicName, subscriptionName, ct);
        }

        public IQueueService Decorate(NamespaceInfo ns, IQueueService queues) => new Queues(queues);
        public ITopicService Decorate(NamespaceInfo ns, ITopicService topics) => new Topics(topics);
        public IMetricsService Decorate(NamespaceInfo ns, IMetricsService metrics) => new Metrics(metrics);
        public IDeletionService Decorate(NamespaceInfo ns, IDeletionService deletion) => new Deletion(deletion);
    }
}
