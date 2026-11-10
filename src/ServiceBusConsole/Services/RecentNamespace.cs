namespace ServiceBusConsole.Services;

/// <summary>A namespace the user opened, with the subscription it was opened from.</summary>
public sealed record RecentNamespace(AzureSubscription Subscription, NamespaceInfo Namespace, DateTimeOffset Opened)
{
    public bool Is(AzureSubscription subscription, NamespaceInfo ns) =>
        Subscription.Id == subscription.Id && Namespace.FullyQualifiedNamespace == ns.FullyQualifiedNamespace;
}
