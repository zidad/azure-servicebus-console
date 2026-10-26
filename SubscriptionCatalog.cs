using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

/// <summary>
/// The Azure subscriptions of the signed-in account, loaded once per run and shared by the connection
/// page and the header dropdown. Each subscription is checked for Service Bus namespaces as soon as it
/// appears: a cached answer applies at once, a live re-check follows in the background. Ones without
/// namespaces are hidden once that is known.
/// </summary>
public sealed class SubscriptionCatalog(INamespaceService service, ILogger<SubscriptionCatalog> logger)
{
    private readonly Lock _sync = new();
    private readonly Dictionary<string, bool> _hasNamespaces = [];
    private readonly Dictionary<string, Task> _checks = [];
    private Task? _load;

    public IReadOnlyList<AzureSubscription> Subscriptions { get; private set; } = [AzureSubscription.Emulator];

    public bool Loading { get; private set; } = true;

    public string? Error { get; private set; }

    /// <summary>Raised on any thread when the list or a namespace check changes.</summary>
    public event Action? Changed;

    /// <summary>True or false once checked; null while unknown. The emulator always has one.</summary>
    public bool? HasNamespaces(AzureSubscription subscription)
    {
        if (subscription.Id is not { } id) return true;
        lock (_sync) return _hasNamespaces.TryGetValue(id, out var has) ? has : null;
    }

    public Task EnsureLoadedAsync()
    {
        lock (_sync) return _load ??= LoadAsync();
    }

    private async Task LoadAsync()
    {
        try
        {
            await foreach (var list in service.ListSubscriptionsAsync())
            {
                Subscriptions = list;
                StartChecks(list);
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Listing subscriptions failed");
            Error = ex.Message;
        }

        Loading = false;
        Changed?.Invoke();

        Task[] checks;
        lock (_sync) checks = [.. _checks.Values];
        await Task.WhenAll(checks);
    }

    private void StartChecks(IEnumerable<AzureSubscription> subscriptions)
    {
        List<string> started = [];
        lock (_sync)
        {
            foreach (var id in subscriptions.Select(s => s.Id).OfType<string>())
            {
                if (_checks.ContainsKey(id)) continue;
                _checks[id] = Task.CompletedTask;
                started.Add(id);
            }
        }

        // Started outside the lock: a cached answer can complete synchronously and take the lock itself.
        foreach (var id in started)
        {
            var check = CheckAsync(id);
            lock (_sync) _checks[id] = check;
        }
    }

    private async Task CheckAsync(string id)
    {
        try
        {
            await foreach (var has in service.HasNamespacesAsync(id))
            {
                lock (_sync) _hasNamespaces[id] = has;
                Changed?.Invoke();
            }
        }
        catch (Exception ex)
        {
            // Unknown stays visible; a subscription we cannot read is not hidden.
            logger.LogWarning(ex, "Checking namespaces of {Subscription} failed", id);
        }
    }
}
