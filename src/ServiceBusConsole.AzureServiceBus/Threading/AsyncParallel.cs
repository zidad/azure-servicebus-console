using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ServiceBusConsole.AzureServiceBus.Threading;

internal static class AsyncParallel
{
    /// <summary>
    /// Projects a stream with at most <paramref name="maxConcurrency"/> calls in flight and yields each
    /// result as soon as it completes, so one slow item does not hold up the rest. Results arrive in
    /// completion order; callers that care about order sort them.
    /// </summary>
    public static IAsyncEnumerable<TResult> MapAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        int maxConcurrency,
        CancellationToken ct = default) =>
        MergeAsync(source.Select(item => OnceAsync(item, selector)), maxConcurrency, ct);

    /// <summary>
    /// Reads up to <paramref name="maxConcurrency"/> streams at once and yields their items as they arrive.
    /// The consumer still receives one item at a time on its own thread, so callers keep their
    /// single-threaded update loop. The first error ends the merge and is rethrown.
    /// </summary>
    public static async IAsyncEnumerable<T> MergeAsync<T>(
        IAsyncEnumerable<IAsyncEnumerable<T>> sources,
        int maxConcurrency,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var results = Channel.CreateBounded<T>(maxConcurrency);
        var producer = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(sources, new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = stop.Token },
                    async (stream, token) =>
                    {
                        await foreach (var item in stream.WithCancellation(token))
                            await results.Writer.WriteAsync(item, token);
                    });
                results.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                results.Writer.TryComplete(ex);
            }
        }, CancellationToken.None);

        try
        {
            await foreach (var result in results.Reader.ReadAllAsync(ct))
                yield return result;
        }
        finally
        {
            // A consumer that stops early, faults or is cancelled stops the producer too; its outcome
            // has already reached the caller through the channel.
            await stop.CancelAsync();
            await producer;
        }
    }

    private static async IAsyncEnumerable<TResult> OnceAsync<TSource, TResult>(
        TSource item, Func<TSource, CancellationToken, Task<TResult>> selector, [EnumeratorCancellation] CancellationToken ct = default)
    {
        yield return await selector(item, ct);
    }
}
