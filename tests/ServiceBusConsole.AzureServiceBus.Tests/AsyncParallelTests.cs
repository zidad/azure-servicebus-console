namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class AsyncParallelTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task MapAsync_YieldsInCompletionOrder_SoASlowItemDoesNotHoldUpTheRest()
    {
        var slow = new TaskCompletionSource();
        var results = new List<int>();

        await foreach (var result in AsyncParallel.MapAsync(Items(1, 2, 3), async (i, token) =>
        {
            if (i == 1) await slow.Task.WaitAsync(token);
            return i;
        }, maxConcurrency: 3, Ct))
        {
            results.Add(result);
            if (results.Count == 2) slow.SetResult();
        }

        // 2 and 3 may finish in either order; the slow first item comes last.
        results.Take(2).ShouldBe([2, 3], ignoreOrder: true);
        results[2].ShouldBe(1);
    }

    [Fact]
    public async Task MapAsync_PropagatesAFailure()
    {
        var run = async () =>
        {
            await foreach (var _ in AsyncParallel.MapAsync(Items(1, 2), (i, _) =>
                i == 2 ? Task.FromException<int>(new InvalidOperationException("boom")) : Task.FromResult(i), maxConcurrency: 2, Ct))
            {
            }
        };

        (await Should.ThrowAsync<InvalidOperationException>(run)).Message.ShouldBe("boom");
    }

    [Fact]
    public async Task MapAsync_RunsAtMostMaxConcurrencyAtOnce()
    {
        var running = 0;
        var peak = 0;

        await foreach (var _ in AsyncParallel.MapAsync(Items([.. Enumerable.Range(1, 20)]), async (i, token) =>
        {
            var now = Interlocked.Increment(ref running);
            InterlockedMax(ref peak, now);
            await Task.Delay(5, token);
            Interlocked.Decrement(ref running);
            return i;
        }, maxConcurrency: 4, Ct))
        {
        }

        peak.ShouldBeLessThanOrEqualTo(4);
    }

    [Fact]
    public async Task MergeAsync_YieldsFromEveryStreamAsItArrives()
    {
        var secondDone = new TaskCompletionSource();

        async IAsyncEnumerable<int> First()
        {
            yield return 1;
            await secondDone.Task.WaitAsync(Ct);
            yield return 3;
        }

        async IAsyncEnumerable<int> Second()
        {
            await Task.Yield();
            yield return 2;
            secondDone.SetResult();
        }

        var merged = await AsyncParallel.MergeAsync(new[] { First(), Second() }.ToAsyncEnumerable(), maxConcurrency: 2, Ct).ToListAsync(Ct);

        merged.ShouldBe([1, 2, 3], ignoreOrder: true);
        merged[^1].ShouldBe(3);
    }

    private static async IAsyncEnumerable<int> Items(params int[] items)
    {
        foreach (var item in items)
        {
            await Task.Yield();
            yield return item;
        }
    }

    private static void InterlockedMax(ref int target, int value)
    {
        int current;
        while ((current = Volatile.Read(ref target)) < value && Interlocked.CompareExchange(ref target, value, current) != current) { }
    }
}
