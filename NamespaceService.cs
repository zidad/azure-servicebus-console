using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class NamespaceService(ArmClient armClient, ILogger<NamespaceService> logger) : INamespaceService
{
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
                Location = ns.Data.Location.DisplayName ?? ns.Data.Location.Name
            };
        }
    }
}
