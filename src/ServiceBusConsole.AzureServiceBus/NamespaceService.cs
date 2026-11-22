using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.ServiceBus;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

internal sealed class NamespaceService(ArmClient armClient, ILogger<NamespaceService> logger) : IConnectionCatalog
{
    // Bounds live namespace checks across all subscriptions; cached answers never wait for this.
    private readonly SemaphoreSlim _checks = new(4);

    public IAsyncEnumerable<ConnectionInfo> GetConnectionsAsync(CancellationToken ct = default) =>
        armClient.GetSubscriptions().GetAllAsync(ct)
            .Select(sub => new ConnectionInfo(sub.Data.DisplayName, sub.Data.SubscriptionId));

    public async Task<string?> GetCurrentConnectionIdAsync(CancellationToken ct = default)
    {
        try
        {
            return await AzureCliProfile.CurrentSubscriptionIdAsync(AzureCliProfile.DefaultPath, ct);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Text.Json.JsonException)
        {
            logger.LogWarning(ex, "Reading the Azure CLI profile failed");
            return null;
        }
    }

    public async IAsyncEnumerable<bool> HasBrokersAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
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

    public IAsyncEnumerable<BrokerInfo> GetBrokersAsync(string subscriptionId, CancellationToken ct = default)
    {
        logger.LogInformation("Listing namespaces for subscription {SubscriptionId}", subscriptionId);
        return Subscription(subscriptionId).GetServiceBusNamespacesAsync(ct).Select(ns => new BrokerInfo
        {
            Name = ns.Data.Name,
            Address = $"{ns.Data.Name}.servicebus.windows.net",
            Properties = NamespaceProperties.Of(ns.Data.Sku?.Name.ToString(), ns.Data.Location.DisplayName ?? ns.Data.Location.Name,
                ns.Id?.ResourceGroupName, ns.Data.Status, ns.Id?.ToString()),
        });
    }

    private Azure.ResourceManager.Resources.SubscriptionResource Subscription(string id) =>
        armClient.GetSubscriptionResource(new ResourceIdentifier($"/subscriptions/{id}"));
}
