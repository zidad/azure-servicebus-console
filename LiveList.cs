using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

/// <summary>
/// The rows of a list page, kept current: the first load shows cached rows then live ones; after that a
/// poll every <see cref="LiveCountsOptions.Interval"/> merges live rows, highlights changed counts and
/// adds or drops entities. Call from the page's dispatcher; callbacks run there too.
/// </summary>
public sealed class LiveList<T>(
    Func<CancellationToken, IAsyncEnumerable<T>> source,
    Func<T, string> key,
    Func<T, bool> isCached,
    CountChanges<T> changes,
    LiveCountsOptions options,
    ILogger logger) : IDisposable
{
    private static readonly TimeSpan FadeStep = TimeSpan.FromMilliseconds(100);

    private readonly RenderThrottle _renderThrottle = new();
    private CancellationTokenSource _cts = new();
    private bool _fading;

    public List<T> All { get; } = [];

    public CountChanges<T> Changes { get; } = changes;

    /// <summary>Re-sort/filter and render; after every batch of rows and every poll.</summary>
    public required Func<Task> Changed { get; init; }

    /// <summary>Render only; during a highlight fade.</summary>
    public required Action Render { get; init; }

    /// <summary>The first load failed; polls only log their errors.</summary>
    public required Func<Exception, Task> LoadFailed { get; init; }

    /// <summary>Loads, then keeps polling until <see cref="Dispose"/> or the next load (F5).</summary>
    public async Task LoadAsync()
    {
        // Take the token for *this* run: a reload replaces the field, and a superseded run
        // must not read the new run's token and conclude it was never cancelled.
        _cts.Cancel();
        var token = (_cts = new CancellationTokenSource()).Token;
        if (await RefreshAsync(token, poll: false)) _ = PollAsync(token);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _cts.Dispose();
    }

    private async Task PollAsync(CancellationToken token)
    {
        try
        {
            while (true)
            {
                await Task.Delay(options.Interval, token);
                await RefreshAsync(token, poll: true);
            }
        }
        catch (OperationCanceledException) { }
    }

    private async Task<bool> RefreshAsync(CancellationToken token, bool poll)
    {
        var live = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        _renderThrottle.Reset();
        var transitionRendered = false;

        try
        {
            await foreach (var row in source(token))
            {
                if (token.IsCancellationRequested) break;
                var cached = isCached(row);
                if (poll && cached) continue;
                if (!cached) live.Add(key(row));
                var idx = All.FindIndex(x => key(x) == key(row));
                if (idx >= 0)
                {
                    if (poll) Changes.Compare(All[idx], row);
                    All[idx] = row;
                }
                else All.Add(row);
                if (poll) continue;

                // Cached rows arrive in one burst, so force a render when the first live
                // row lands; after that the throttle paces the (network-bound) stream.
                var firstLive = !cached && !transitionRendered;
                if (firstLive) transitionRendered = true;
                if (firstLive || _renderThrottle.ShouldRender()) await Changed();
            }

            if (token.IsCancellationRequested) return false;
            All.RemoveAll(x => !live.Contains(key(x)));
            await Changed();
            if (poll) _ = FadeAsync(token);
            return true;
        }
        catch (OperationCanceledException) { return false; }
        catch (Exception ex) when (poll)
        {
            logger.LogWarning(ex, "Polling {Rows} failed; retrying", typeof(T).Name);
            return false;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Loading {Rows} failed", typeof(T).Name);
            await LoadFailed(ex);
            return false;
        }
    }

    private async Task FadeAsync(CancellationToken token)
    {
        if (_fading) return;
        _fading = true;
        try
        {
            while (Changes.IsFading())
            {
                await Task.Delay(FadeStep, token);
                Render();
            }
        }
        catch (OperationCanceledException) { }
        finally { _fading = false; }
    }
}
