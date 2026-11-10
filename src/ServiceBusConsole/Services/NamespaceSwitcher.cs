using Microsoft.AspNetCore.Components;

namespace ServiceBusConsole.Services;

/// <summary>Opens a namespace the way the user asked for it: switches the connection to it and records it in <see cref="RecentNamespaces"/>.</summary>
public sealed class NamespaceSwitcher(
    ServiceBusConnection connection,
    NavigationState navState,
    MessageBrowserStore store,
    RecentNamespaces recents,
    NavigationManager nav)
{
    public bool IsCurrent(AzureSubscription subscription, NamespaceInfo ns) =>
        connection.CurrentNamespace == ns.FullyQualifiedNamespace && navState.AzureSubscription == subscription;

    /// <summary>Makes <paramref name="ns"/> the connection, unless it is already, and records the open.</summary>
    public async Task SwitchAsync(AzureSubscription subscription, NamespaceInfo ns)
    {
        _ = recents.RecordAsync(subscription, ns);
        if (IsCurrent(subscription, ns)) return;
        // The store keys browsers by entity name only; a same-named one elsewhere must not reuse its locks.
        await store.CloseAsync();
        navState.AzureSubscription = subscription;
        connection.SetNamespace(ns);
    }

    /// <summary><see cref="SwitchAsync"/>, then <see cref="Show"/> <paramref name="route"/>.</summary>
    public async Task OpenAsync(AzureSubscription subscription, NamespaceInfo ns, string route)
    {
        await SwitchAsync(subscription, ns);
        Show(route);
    }

    /// <summary>Navigates to <paramref name="route"/>, or remounts it when it is the page shown.</summary>
    public void Show(string route)
    {
        if ("/" + nav.ToBaseRelativePath(nav.Uri) == route) navState.Reload();
        else nav.NavigateTo(route);
    }
}
