namespace ServiceBusConsole.Services;

public interface INamespaceService
{
    IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync(CancellationToken ct = default);

    /// <summary>Whether the subscription has any Service Bus namespace: cached answer first, then live.</summary>
    IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, CancellationToken ct = default);
    IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, CancellationToken ct = default);
}
