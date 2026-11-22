using System.Collections.Concurrent;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

/// <summary>The connection tree from the cache alone, while every live Azure call hangs.</summary>
public sealed class CachedConnectionTreeTests
{
    private static readonly ConnectionInfo Billing = new("Contoso Billing", "00000000-0000-4000-8000-000000000009");
    private static readonly ConnectionInfo Development = new("Contoso Development", "00000000-0000-4000-8000-000000000001");
    private static readonly BrokerInfo Orders = AppHarness.AzureNamespace;

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
        var service = new CachingConnectionCatalog(new HangingNamespaceService(), cache);
        var catalog = new ConnectionList(service, NullLogger<ConnectionList>.Instance);

        _ = catalog.EnsureLoadedAsync();

        await WaitAsync(() => catalog.Connections.Count == 3 && catalog.HasBrokers(Billing) == false && catalog.HasBrokers(Development) == true);
        catalog.Loading.ShouldBeTrue();
    }

    [Test]
    public async Task ConnectionPage_ShowsTheCachedTree_WhileLiveCallsHang()
    {
        var app = new AppHarness();
        app.ConnectionCatalog = new CachingConnectionCatalog(new HangingNamespaceService(), await SeededCacheAsync());
        await using var terminal = await app.StartAsync(height: 40);

        // The emulator's namespace is added in the background too; until then it says "Discovering".
        var frame = await terminal.WaitUntilAsync(
            f => f.ContainsText("Contoso Development") && f.ContainsText(Orders.Name) && f.ContainsText("🌐 localhost") && !f.ContainsText("Contoso Billing"),
            TimeSpan.FromSeconds(2), cancellationToken: Ct);
        frame.ContainsText("Discovering namespaces").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task Loading_RunsOffTheRenderersContext()
    {
        var live = new HangingNamespaceService();
        var app = new AppHarness { ConnectionCatalog = new CachingConnectionCatalog(live, await SeededCacheAsync()) };
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
    private sealed class HangingNamespaceService : IConnectionCatalog
    {
        public ConcurrentQueue<SynchronizationContext?> Calls { get; } = new();

        public async IAsyncEnumerable<ConnectionInfo> GetConnectionsAsync([System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }

        public Task<string?> GetCurrentConnectionIdAsync(CancellationToken ct = default) => Task.FromResult<string?>(null);

        public async IAsyncEnumerable<BrokerInfo> GetBrokersAsync(string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }

        public async IAsyncEnumerable<bool> HasBrokersAsync(string subscriptionId, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
        {
            Calls.Enqueue(SynchronizationContext.Current);
            await Task.Delay(Timeout.Infinite, ct);
            yield break;
        }
    }
}
