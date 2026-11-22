namespace ServiceBusConsole.Abstractions;

public interface IConnectionCatalog
{
    IAsyncEnumerable<ConnectionInfo> GetConnectionsAsync(CancellationToken ct = default);

    /// <summary>The Azure CLI's current subscription (<c>az account set</c>); null when unknown.</summary>
    Task<string?> GetCurrentConnectionIdAsync(CancellationToken ct = default);

    /// <summary>Whether the subscription has any Service Bus namespace: cached answer first, then live.</summary>
    IAsyncEnumerable<bool> HasBrokersAsync(string subscriptionId, CancellationToken ct = default);
    IAsyncEnumerable<BrokerInfo> GetBrokersAsync(string subscriptionId, CancellationToken ct = default);
}
