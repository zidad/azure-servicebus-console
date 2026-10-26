namespace ServiceBusConsole.Services;

public static class AzureSubscriptions
{
    public static NamespaceInfo EmulatorNamespace { get; } = new()
    {
        Name = "localhost", FullyQualifiedNamespace = "localhost", ResourceGroup = "", Location = "",
    };

    /// <summary>
    /// The emulator, then the signed-in account's subscriptions by name. Yields the whole list each time
    /// it grows (cached entries first, then live ones).
    /// </summary>
    public static async IAsyncEnumerable<IReadOnlyList<AzureSubscription>> ListSubscriptionsAsync(
        this INamespaceService service,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var byId = new Dictionary<string, AzureSubscription>();
        yield return [AzureSubscription.Emulator];
        await foreach (var sub in service.GetSubscriptionsAsync(ct))
        {
            if (sub.Id is null) continue;
            byId[sub.Id] = sub;
            yield return [AzureSubscription.Emulator, .. byId.Values.OrderBy(s => s.Name, StringComparer.CurrentCultureIgnoreCase)];
        }
    }

    /// <summary>Namespaces of <paramref name="subscription"/>: cached first, then live.</summary>
    public static async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(
        this INamespaceService service, AzureSubscription subscription,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        if (subscription.Id is null)
        {
            yield return EmulatorNamespace;
            yield break;
        }
        await foreach (var ns in service.GetNamespacesAsync(subscription.Id, ct))
            yield return ns;
    }
}
