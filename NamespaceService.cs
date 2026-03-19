using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class NamespaceService(ArmClient armClient, ILogger<NamespaceService> logger)
{
    public async Task<List<NamespaceInfo>> GetNamespacesAsync(string subscriptionId)
    {
        logger.LogInformation("Listing namespaces for subscription {SubscriptionId}", subscriptionId);
        var namespaces = new List<NamespaceInfo>();
        var sub = armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{subscriptionId}"));

        await foreach (var ns in sub.GetServiceBusNamespacesAsync())
        {
            logger.LogDebug("Found namespace: {Name}", ns.Data.Name);
            namespaces.Add(new NamespaceInfo
            {
                Name = ns.Data.Name,
                FullyQualifiedNamespace = $"{ns.Data.Name}.servicebus.windows.net",
                ResourceGroup = ns.Id?.ResourceGroupName ?? "",
                Location = ns.Data.Location.DisplayName ?? ns.Data.Location.Name
            });
        }

        logger.LogInformation("Found {Count} namespaces", namespaces.Count);
        return namespaces;
    }
}
