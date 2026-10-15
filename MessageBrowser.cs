using System.Collections.Concurrent;
using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole;

public enum BrowseMode { Peek, Locked, Received }

public sealed class BulkOperationException(string message) : Exception(message);

/// <summary>
/// The message list of one queue or subscription (active or DLQ), its selection, and the
/// receiver that holds any PeekLocked messages. Outlives the page so the message detail page
/// can act on the same locks. List mutations happen on the caller's (dispatcher) thread;
/// <see cref="Changed"/> may fire from background threads.
/// </summary>
public sealed class MessageBrowser(MessageSource source, IMessageService service) : IAsyncDisposable
{
    private const string DeadLetterReason = "Dead-lettered manually";
    private LockedMessages? _locked;
    private readonly RenderThrottle _progressThrottle = new(100);

    public MessageSource Source { get; } = source;
    public BrowseMode Mode { get; private set; } = BrowseMode.Peek;
    public List<MessageInfo> Messages { get; private set; } = [];
    public HashSet<long> Selected { get; } = [];
    public int Cursor { get; set; }
    public string? Progress { get; private set; }

    public event Action? Changed;

    public MessageInfo? Focused => Messages.ElementAtOrDefault(Cursor);

    /// <summary>The selected messages in list order, or the focused one when nothing is selected.</summary>
    public IReadOnlyList<MessageInfo> Targets =>
        Selected.Count > 0 ? Messages.Where(IsSelected).ToList()
        : Focused is { } f ? [f] : [];

    public bool IsSelected(MessageInfo m) => Selected.Contains(m.SequenceNumber);
    public bool IsLockHeld(MessageInfo m) => _locked?.IsHeld(m.SequenceNumber) == true;
    public int LockedCount => _locked is null ? 0 : Messages.Count(IsLockHeld);

    public void Toggle(MessageInfo m)
    {
        if (!Selected.Remove(m.SequenceNumber)) Selected.Add(m.SequenceNumber);
    }

    public void SelectAll() => Selected.UnionWith(Messages.Select(m => m.SequenceNumber));

    public void InvertSelection()
    {
        var all = Messages.Select(m => m.SequenceNumber).ToHashSet();
        all.ExceptWith(Selected);
        Selected.Clear();
        Selected.UnionWith(all);
    }

    public async Task PeekAsync(int count, long? fromSequenceNumber)
    {
        await ReleaseLocksAsync();
        Load(await service.PeekAsync(Source, count, fromSequenceNumber), BrowseMode.Peek);
    }

    public async Task ReceiveAsync(int count, bool deleteOnReceive)
    {
        await ReleaseLocksAsync();
        if (deleteOnReceive)
        {
            Load(await service.ReceiveAndDeleteAsync(Source, count), BrowseMode.Received);
            return;
        }
        var locked = await service.ReceiveLockedAsync(Source, count);
        locked.LockLost += _ => Changed?.Invoke();
        _locked = locked;
        Load(locked.Messages.Select(m => new MessageInfo(m, MessageState.Locked)).ToList(), BrowseMode.Locked);
    }

    public Task CompleteAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Completing", "Completed", targets, (l, m, ct) => l.CompleteAsync(m, ct));

    public Task AbandonAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Abandoning", "Abandoned", targets, (l, m, ct) => l.AbandonAsync(m, ct));

    public Task DeadLetterAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Dead-lettering", "Dead-lettered", targets, (l, m, ct) => l.DeadLetterAsync(m, DeadLetterReason, ct));

    /// <summary>
    /// Sends copies to the queue/topic and removes the originals: completes them when locked,
    /// or just drops them from the list when they were already received and deleted.
    /// </summary>
    public async Task RequeueAsync(IReadOnlyList<MessageInfo> targets)
    {
        var failures = new List<(MessageInfo Message, string Reason)>();
        var candidates = targets.OrderBy(m => m.SequenceNumber).ToList();
        if (Mode == BrowseMode.Locked)
        {
            failures.AddRange(candidates.Where(m => !IsLockHeld(m)).Select(m => (m, "lock lost")));
            candidates = candidates.Where(IsLockHeld).ToList();
        }
        else if (Mode != BrowseMode.Received)
        {
            throw new InvalidOperationException("Requeue needs received messages; use Resend for peeked ones");
        }

        var sent = await SendAsync("Requeuing", candidates, failures);
        var removed = sent;
        if (Mode == BrowseMode.Locked && _locked is { } locked && sent.Count > 0)
        {
            var completeFailures = new ConcurrentBag<(MessageInfo, string)>();
            removed = await RunParallelAsync("Completing", sent, async (m, ct) =>
            {
                try { await locked.CompleteAsync(m, ct); return true; }
                catch (Exception ex) { completeFailures.Add((m, $"sent, but original not completed: {Describe(ex)}")); return false; }
            });
            failures.AddRange(completeFailures);
        }
        Finish("Requeued", targets.Count, removed, failures);
    }

    /// <summary>Sends copies to the queue/topic and leaves the originals where they are.</summary>
    public async Task ResendAsync(IReadOnlyList<MessageInfo> targets)
    {
        var failures = new List<(MessageInfo, string)>();
        var sent = await SendAsync("Sending", targets.OrderBy(m => m.SequenceNumber).ToList(), failures);
        Progress = null;
        Selected.Clear();
        Selected.UnionWith(failures.Select(f => f.Item1.SequenceNumber));
        Changed?.Invoke();
        if (failures.Count > 0)
            throw new BulkOperationException(Summary("Sent", targets.Count, sent.Count, failures));
    }

    public async Task<int> PurgeAsync()
    {
        await ReleaseLocksAsync();
        Progress = "Purging…";
        Changed?.Invoke();
        try
        {
            var count = await service.PurgeMessagesAsync(Source);
            Load([], Mode);
            return count;
        }
        finally
        {
            Progress = null;
            Changed?.Invoke();
        }
    }

    public async Task ReleaseLocksAsync()
    {
        if (_locked is null) return;
        var locked = _locked;
        _locked = null;
        await locked.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(ReleaseLocksAsync());

    private void Load(List<MessageInfo> messages, BrowseMode mode)
    {
        Messages = messages;
        Mode = mode;
        Selected.Clear();
        Cursor = 0;
        Progress = null;
        Changed?.Invoke();
    }

    private async Task<List<MessageInfo>> SendAsync(string verb, List<MessageInfo> messages, List<(MessageInfo, string)> failures)
    {
        if (messages.Count == 0) return [];
        var sent = 0;
        SetProgress(verb, 0, messages.Count);
        try
        {
            await service.SendCopiesAsync(Source, messages, n => { sent = n; SetProgress(verb, n, messages.Count); });
        }
        catch (Exception ex)
        {
            failures.AddRange(messages.Skip(sent).Select(m => (m, $"not sent: {Describe(ex)}")));
        }
        return messages.Take(sent).ToList();
    }

    private async Task SettleAsync(string verb, string pastVerb, IReadOnlyList<MessageInfo> targets, Func<LockedMessages, MessageInfo, CancellationToken, Task> settle)
    {
        var locked = _locked ?? throw new InvalidOperationException("No locked messages. Receive in PeekLock mode first.");
        var failures = new ConcurrentBag<(MessageInfo, string)>();
        var done = await RunParallelAsync(verb, targets, async (m, ct) =>
        {
            try { await settle(locked, m, ct); return true; }
            catch (Exception ex) { failures.Add((m, Describe(ex))); return false; }
        });
        Finish(pastVerb, targets.Count, done, failures.ToList());
    }

    private async Task<List<MessageInfo>> RunParallelAsync(string verb, IReadOnlyList<MessageInfo> items, Func<MessageInfo, CancellationToken, Task<bool>> op)
    {
        var ok = new ConcurrentBag<MessageInfo>();
        var done = 0;
        SetProgress(verb, 0, items.Count);
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (m, ct) =>
        {
            if (await op(m, ct)) ok.Add(m);
            SetProgress(verb, Interlocked.Increment(ref done), items.Count);
        });
        return ok.ToList();
    }

    private void Finish(string pastVerb, int total, IReadOnlyCollection<MessageInfo> removed, IReadOnlyCollection<(MessageInfo Message, string Reason)> failures)
    {
        var removedIds = removed.Select(m => m.SequenceNumber).ToHashSet();
        var focused = Focused;
        Messages = Messages.Where(m => !removedIds.Contains(m.SequenceNumber)).ToList();
        Cursor = focused is not null && !removedIds.Contains(focused.SequenceNumber)
            ? Messages.IndexOf(focused)
            : Math.Min(Cursor, Math.Max(0, Messages.Count - 1));
        Selected.Clear();
        Selected.UnionWith(failures.Select(f => f.Message.SequenceNumber));
        Progress = null;
        Changed?.Invoke();
        if (failures.Count > 0)
            throw new BulkOperationException(Summary(pastVerb, total, removed.Count, failures));
    }

    private static string Summary(string pastVerb, int total, int succeeded, IReadOnlyCollection<(MessageInfo Message, string Reason)> failures)
    {
        var examples = string.Join("; ", failures.OrderBy(f => f.Message.SequenceNumber).Take(3)
            .Select(f => $"#{f.Message.SequenceNumber} {f.Reason}"));
        var more = failures.Count > 3 ? $" (+{failures.Count - 3} more)" : "";
        return $"{pastVerb} {succeeded} of {total}. Failed messages stay selected: {examples}{more}";
    }

    private static string Describe(Exception ex) => ex switch
    {
        ServiceBusException { Reason: ServiceBusFailureReason.MessageLockLost } => "lock lost",
        _ => ex.Message.Split('\n')[0],
    };

    private void SetProgress(string verb, int done, int total)
    {
        Progress = $"{verb} {done}/{total}…";
        if (done == 0 || done == total || _progressThrottle.ShouldRender())
            Changed?.Invoke();
    }
}

/// <summary>Keeps the one message browser in use, so its locks survive a trip to the detail page.</summary>
public sealed class MessageBrowserStore(IMessageService service) : IAsyncDisposable
{
    public MessageBrowser? Current { get; private set; }

    /// <summary>Returns the browser for <paramref name="source"/>; <c>IsNew</c> is false when it was already open.
    /// Opening another source releases the previous one's locks in the background.</summary>
    public (MessageBrowser Browser, bool IsNew) Open(MessageSource source)
    {
        if (Current is { } current && current.Source == source) return (current, false);
        _ = CloseAsync();
        Current = new MessageBrowser(source, service);
        return (Current, true);
    }

    public async Task CloseAsync()
    {
        if (Current is null) return;
        var current = Current;
        Current = null;
        await current.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(CloseAsync());
}
