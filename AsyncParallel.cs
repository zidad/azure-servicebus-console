using System.Runtime.CompilerServices;

namespace ServiceBusConsole;

public static class AsyncParallel
{
    /// <summary>
    /// Projects a stream with a bounded number of overlapping calls, yielding results in
    /// source order. The consumer still receives one item at a time on its own thread, so
    /// callers keep their single-threaded update loop — only the round-trips overlap.
    /// </summary>
    public static async IAsyncEnumerable<TResult> MapAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        int maxConcurrency,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var inFlight = new Queue<Task<TResult>>(maxConcurrency);
        try
        {
            await foreach (var item in source.WithCancellation(ct))
            {
                inFlight.Enqueue(selector(item, ct));
                if (inFlight.Count >= maxConcurrency)
                    yield return await inFlight.Dequeue();
            }

            while (inFlight.Count > 0)
                yield return await inFlight.Dequeue();
        }
        finally
        {
            // Drain whatever is still running after a fault, a cancellation, or a consumer
            // that stopped early, so no task exception is left unobserved.
            while (inFlight.Count > 0)
            {
                try { await inFlight.Dequeue(); }
                catch { /* the failure that got us here has already reached the caller */ }
            }
        }
    }
}
