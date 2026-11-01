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

        using var list = new LiveList<string>(CachedThenFirewall, x => x, _ => true, new CountChanges<string>(x => x),
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
}
