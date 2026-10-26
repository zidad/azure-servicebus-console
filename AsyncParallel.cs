using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ServiceBusConsole;

public static class AsyncParallel
{
    /// <summary>
    /// Projects a stream with at most <paramref name="maxConcurrency"/> calls in flight and yields each
    /// result as soon as it completes, so one slow item does not hold up the rest. The consumer still
    /// receives one item at a time on its own thread, so callers keep their single-threaded update loop.
    /// Results arrive in completion order; callers that care about order sort them.
    /// </summary>
    public static async IAsyncEnumerable<TResult> MapAsync<TSource, TResult>(
        IAsyncEnumerable<TSource> source,
        Func<TSource, CancellationToken, Task<TResult>> selector,
        int maxConcurrency,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        using var stop = CancellationTokenSource.CreateLinkedTokenSource(ct);
        var results = Channel.CreateBounded<TResult>(maxConcurrency);
        var producer = Task.Run(async () =>
        {
            try
            {
                await Parallel.ForEachAsync(source, new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = stop.Token },
                    async (item, token) => await results.Writer.WriteAsync(await selector(item, token), token));
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
}
