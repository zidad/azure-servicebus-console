using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

internal sealed class NamespaceService(ArmClient armClient, ILogger<NamespaceService> logger) : INamespaceService
{
    // Bounds live namespace checks across all subscriptions; cached answers never wait for this.
    private readonly SemaphoreSlim _checks = new(4);

    public IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default) =>
        armClient.GetSubscriptions().GetAllAsync(ct)
            .Select(sub => new AzureSubscription(sub.Data.DisplayName, sub.Data.SubscriptionId));

    public async IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        bool has;
        await _checks.WaitAsync(ct);
        try
        {
            has = await Subscription(subscriptionId).GetServiceBusNamespacesAsync(ct).AnyAsync(ct);
        }
        finally
        {
            _checks.Release();
        }
        yield return has;
    }

    public IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default)
    {
        logger.LogInformation("Listing namespaces for subscription {SubscriptionId}", subscriptionId);
        return Subscription(subscriptionId).GetServiceBusNamespacesAsync(ct).Select(ns => new NamespaceInfo
        {
            Name = ns.Data.Name,
            FullyQualifiedNamespace = $"{ns.Data.Name}.servicebus.windows.net",
            ResourceGroup = ns.Id?.ResourceGroupName ?? "",
            Location = ns.Data.Location.DisplayName ?? ns.Data.Location.Name,
            Sku = ns.Data.Sku?.Name.ToString(),
            Status = ns.Data.Status,
        });
    }

    private Azure.ResourceManager.Resources.SubscriptionResource Subscription(string id) =>
        armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{id}"));
}
