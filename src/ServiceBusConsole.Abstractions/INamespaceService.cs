namespace ServiceBusConsole.Abstractions;

public interface INamespaceService
{
    IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>The Azure CLI's current subscription (<c>az account set</c>); null when unknown.</summary>
    Task<string?> GetCurrentSubscriptionIdAsync(CancellationToken ct = default);

    /// <summary>Whether the subscription has any Service Bus namespace: cached answer first, then live.</summary>
    IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, CancellationToken ct = default);
    IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);
}
