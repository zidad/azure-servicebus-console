using System.Runtime.CompilerServices;
using EasyNetQ.Management.Client;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>The configured server, its virtual hosts as its namespaces.</summary>
internal sealed class ServerCatalog(RabbitMqOptions options, IManagementClient management, ILogger<ServerCatalog> logger) : IConnectionCatalog
{
    private AzureSubscription Server { get; } = new(options.Management.Authority, options.Management.AbsoluteUri);

    public IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default) => new[] { Server }.ToAsyncEnumerable();

    public Task<string?> GetCurrentSubscriptionIdAsync(CancellationToken ct = default) => Task.FromResult(Server.Id);

    public IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, CancellationToken ct = default) =>
        new[] { subscriptionId == Server.Id }.ToAsyncEnumerable();

    public async IAsyncEnumerable<BrokerInfo> GetNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (subscriptionId != Server.Id) yield break;
        logger.LogInformation("Listing virtual hosts on {Server}", options.Management);
        foreach (var vhost in await management.GetVhostsAsync(ct))
            yield return Vhost(vhost.Name);
    }

    private BrokerInfo Vhost(string name) => new()
    {
        Name = name,
        FullyQualifiedNamespace = $"{options.Management.Authority}/{Uri.EscapeDataString(name)}",
    };
}
