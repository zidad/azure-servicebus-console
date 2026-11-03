using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

/// <summary>The connection tree from the cache alone, while every live Azure call hangs.</summary>
public sealed class CachedConnectionTreeTests
{
    private static readonly AzureSubscription Billing = new("Contoso Billing", "00000000-0000-4000-8000-000000000009");
    private static readonly AzureSubscription Development = new("Contoso Development", "00000000-0000-4000-8000-000000000001");
    private static readonly NamespaceInfo Orders = AppHarness.AzureNamespace;

    private static async Task<FileCache> SeededCacheAsync()
    {
        var cache = new FileCache(NullLogger<FileCache>.Instance, Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N")));
        await cache.SaveAsync("subscriptions.json", [Billing, Development]);
        await cache.SaveAsync($"{Billing.Id}/has-namespaces.json", [false]);
        await cache.SaveAsync($"{Development.Id}/has-namespaces.json", [true]);
        await cache.SaveAsync($"{Development.Id}/namespaces.json", [Orders]);
        return cache;
    }

    [Test]
    public async Task Catalog_AppliesCachedAnswers_WhileLiveCallsHang()
    {
        var cache = await SeededCacheAsync();
        var service = new CachingNamespaceService(new HangingNamespaceService(), cache);
        var catalog = new SubscriptionCatalog(service, NullLogger<SubscriptionCatalog>.Instance);

        _ = catalog.EnsureLoadedAsync();

        await WaitAsync(() => catalog.Subscriptions.Count == 3 && catalog.HasNamespaces(Billing) == false && catalog.HasNamespaces(Development) == true);
        catalog.Loading.ShouldBeTrue();
    }

    [Test]
    public async Task ConnectionPage_ShowsTheCachedTree_WhileLiveCallsHang()
    {
        var app = new AppHarness();
        app.NamespaceService = new CachingNamespaceService(new HangingNamespaceService(), await SeededCacheAsync());
        await using var terminal = await app.StartAsync(height: 40);

        var frame = await terminal.WaitUntilAsync(
            f => f.ContainsText("Contoso Development") && f.ContainsText(Orders.Name) && !f.ContainsText("Contoso Billing"),
            TimeSpan.FromSeconds(2), cancellationToken: Ct);
        frame.ContainsText("Discovering namespaces").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task Loading_RunsOffTheRenderersContext()
    {
        var live = new HangingNamespaceService();
        var app = new AppHarness { NamespaceService = new CachingNamespaceService(live, await SeededCacheAsync()) };
        await using var terminal = await app.StartAsync(height: 40);

        await terminal.WaitUntilAsync(f => f.ContainsText(Orders.Name), TimeSpan.FromSeconds(2), cancellationToken: Ct);
        await WaitAsync(() => live.Calls.Count >= 3);

        live.Calls.ShouldAllBe(context => context == null);
    }

    private static async Task WaitAsync(Func<bool> condition)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(2));
        while (!condition()) await Task.Delay(10, timeout.Token);
    }

    /// <summary>Every live call hangs; <see cref="Calls"/> records the synchronization context each started on.</summary>
    private sealed class HangingNamespaceService : INamespaceService
    {
        public ConcurrentQueue<SynchronizationContext?> Calls { get; } = new();

        public async IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }

        public Task<string?> GetCurrentSubscriptionIdAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }

        public async IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }
    }
}
