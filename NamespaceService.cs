using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class NamespaceService(ArmClient armClient, ILogger<NamespaceService> logger) : INamespaceService
{
    // Bounds live namespace checks across all subscriptions; cached answers never wait for this.
    private readonly SemaphoreSlim _checks = new(4);

    public async IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing subscriptions");
        await foreach (var sub in armClient.GetSubscriptions().GetAllAsync(ct))
            yield return new AzureSubscription(sub.Data.DisplayName, sub.Data.SubscriptionId);
    }

    public async IAsyncEnumerable<bool> HasNamespacesAsync(
        string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var sub = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));
        var has = false;
        await _checks.WaitAsync(ct);
        try
        {
            await foreach (var _ in sub.GetServiceBusNamespacesAsync().WithCancellation(ct))
            {
                has = true;
                break;
            }
        }
        finally
        {
            _checks.Release();
        }
        yield return has;
    }

    public async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(
        string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        logger.LogInformation("Listing namespaces for subscription {SubscriptionId}", subscriptionId);
        var sub = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        await foreach (var ns in sub.GetServiceBusNamespacesAsync().WithCancellation(ct))
        {
            logger.LogDebug("Found namespace: {Name}", ns.Data.Name);
            yield return new NamespaceInfo
            {
                Name = ns.Data.Name,
                FullyQualifiedNamespace = $"{ns.Data.Name}.servicebus.windows.net",
                ResourceGroup = ns.Id?.ResourceGroupName ?? "",
                Location = ns.Data.Location.DisplayName ?? ns.Data.Location.Name,
                Sku = ns.Data.Sku?.Name.ToString(),
                Status = ns.Data.Status
            };
        }
    }
}
