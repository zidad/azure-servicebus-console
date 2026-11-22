using Microsoft.AspNetCore.Components;

namespace ServiceBusConsole.Services;

/// <summary>Opens a namespace the way the user asked for it: switches the current to it and records it in <see cref="RecentBrokers"/>.</summary>
public sealed class BrokerSwitcher(
    CurrentBroker current,
    NavigationState navState,
    MessageBrowserStore store,
    RecentBrokers recents,
    NavigationManager nav)
{
    public bool IsCurrent(ConnectionInfo subscription, BrokerInfo ns) =>
        current.Address == ns.Address && navState.Connection == subscription;

    /// <summary>Makes <paramref name="ns"/> the current, unless it is already, and records the open.</summary>
    public async Task SwitchAsync(ConnectionInfo subscription, BrokerInfo ns)
    {
        _ = recents.RecordAsync(subscription, ns);
        if (IsCurrent(subscription, ns)) return;
        // The store keys browsers by entity name only; a same-named one elsewhere must not reuse its locks.
        await store.CloseAsync();
        navState.Connection = subscription;
        current.Set(ns);
    }

    /// <summary><see cref="SwitchAsync"/>, then <see cref="Show"/> <paramref name="route"/>.</summary>
    public async Task OpenAsync(ConnectionInfo subscription, BrokerInfo ns, string route)
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
