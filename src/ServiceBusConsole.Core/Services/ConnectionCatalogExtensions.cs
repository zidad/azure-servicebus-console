namespace ServiceBusConsole.Core.Services;

public static class ConnectionCatalogExtensions
{
    /// <summary>
    /// The emulator, then the signed-in account's subscriptions by name. Yields the whole list each time
    /// it grows (cached entries first, then live ones).
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<ConnectionInfo>> ListConnectionsAsync(
        this IConnectionCatalog service,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var byId = new Dictionary<string, ConnectionInfo>();
        yield return [ConnectionInfo.Emulator];
        await foreach (var sub in service.GetConnectionsAsync(ct))
        {
            if (sub.Id is null) continue;
            byId[sub.Id] = sub;
            yield return [ConnectionInfo.Emulator, .. byId.Values.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>Namespaces of <paramref name="subscription"/>: cached first, then live.</summary>
    public static IAsyncEnumerable<BrokerInfo> GetBrokersAsync(
        this IConnectionCatalog service, ConnectionInfo subscription, CancellationToken ct = default) =>
        subscription.Id is { } id
            ? service.GetBrokersAsync(id, ct)
            : new[] { BrokerInfo.Emulator }.ToAsyncEnumerable();
}
