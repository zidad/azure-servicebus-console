using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

public sealed class LiveListTests
{
    [Test]
    public async Task FailedLiveLoad_ShowsEveryCachedRowFirst()
    {
        var yielded = 0;
        var seenAtChanged = new List<int>();
        Exception? failure = null;

        async IAsyncEnumerable<string> CachedThenFirewall([EnumeratorCancellation] CancellationToken ct)
        {
            foreach (var name in new[] { "audit", "billing", "orders" })
            {
                yielded++;
                yield return name;
            }
            await Task.Yield();
            throw new InvalidOperationException("Ip has been prevented to connect to the endpoint");
        }

        using var list = new LiveList<string>(CachedThenFirewall, x => x, _ => true, x => x, new CountChanges<string>(x => x),
            new LiveCountsOptions(), NullLogger.Instance)
        {
            Changed = () => { seenAtChanged.Add(yielded); return Task.CompletedTask; },
            Render = () => { },
            LoadFailed = ex => { failure = ex; return Task.CompletedTask; },
        };

        await list.LoadAsync();

        failure.ShouldNotBeNull();
        seenAtChanged.ShouldNotBeEmpty();
        seenAtChanged[^1].ShouldBe(3);
        list.All.Count.ShouldBe(3);
    }

    [Test]
    public async Task FailedLiveLoad_StopsTheCachedRowsRefreshing()
    {
        async IAsyncEnumerable<QueueInfo> CachedThenFirewall([EnumeratorCancellation] CancellationToken ct)
        {
            yield return new QueueInfo { Name = "orders", IsRefreshing = true };
            yield return new QueueInfo { Name = "billing", IsRefreshing = true };
            await Task.Yield();
            throw new InvalidOperationException("Ip has been prevented to connect to the endpoint");
        }

        using var list = new LiveList<QueueInfo>(CachedThenFirewall, q => q.Name, q => q.IsRefreshing,
            q => q with { IsRefreshing = false }, new CountChanges<QueueInfo>(q => q.Name), new LiveCountsOptions(), NullLogger.Instance)
        {
            Changed = () => Task.CompletedTask,
            Render = () => { },
            LoadFailed = _ => Task.CompletedTask,
        };

        await list.LoadAsync();

        list.All.Select(q => q.Name).ShouldBe(["orders", "billing"]);
        list.All.ShouldAllBe(q => !q.IsRefreshing);
    }
}
