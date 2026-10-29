using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ServiceBusConsole.Messaging;

/// <summary>
/// The message list of one queue or subscription (active or DLQ), its selection, and the
/// receiver that holds any PeekLocked messages. Outlives the page so the message detail page
/// can act on the same locks. List mutations happen on the caller's (dispatcher) thread;
/// <see cref="Changed"/> may fire from background threads.
/// </summary>
public sealed class MessageBrowser(MessageSource source, IMessageService service) : IAsyncDisposable
{
    private const string DeadLetterReason = "Dead-lettered manually";
    private ILockedMessages? _locked;
    private readonly RenderThrottle _progressThrottle = new(100);

    public MessageSource Source { get; } = source;
    public BrowseMode Mode { get; private set; } = BrowseMode.Peek;
    public IReadOnlyList<MessageInfo> Messages { get; private set; } = [];
    public HashSet<long> Selected { get; } = [];
    public int Cursor { get; set; }
    /// <summary>The batch operation in progress or finished (until <see cref="Dismiss"/>), for the progress dialog; null when idle.</summary>
    public OperationProgress? Operation { get; private set; }

    public event Action? Changed;

    /// <summary>Closes a finished operation's dialog.</summary>
    public void Dismiss()
    {
        if (Operation is not { Finished: true }) return;
        Operation = null;
        Changed?.Invoke();
    }

    public MessageInfo? Focused => Messages.ElementAtOrDefault(Cursor);

    /// <summary>The selected messages in list order, or the focused one when nothing is selected.</summary>
    public IReadOnlyList<MessageInfo> Targets =>
        Selected.Count > 0 ? [.. Messages.Where(IsSelected)]
        : Focused is { } f ? [f] : [];

    public bool IsSelected(MessageInfo m) => Selected.Contains(m.SequenceNumber);
    public bool IsLockHeld(MessageInfo m) => _locked?.IsHeld(m.SequenceNumber) == true;
    public int LockedCount => _locked is null ? 0 : Messages.Count(IsLockHeld);

    public void Toggle(MessageInfo m)
    {
        if (!Selected.Remove(m.SequenceNumber)) Selected.Add(m.SequenceNumber);
    }

    public void SelectAll() => Selected.UnionWith(Messages.Select(m => m.SequenceNumber));

    public bool AllSelected => Messages.Count > 0 && Messages.All(IsSelected);

    /// <summary>Selects every message, or clears the selection when all are already selected.</summary>
    public void ToggleAll()
    {
        if (AllSelected) Selected.Clear();
        else SelectAll();
    }

    public void InvertSelection()
    {
        var inverted = Messages.Select(m => m.SequenceNumber).Where(id => !Selected.Contains(id)).ToList();
        Selected.Clear();
        Selected.UnionWith(inverted);
    }

    /// <summary>Peeks into the list page by page; cancelling keeps what arrived.</summary>
    public async Task PeekAsync(int count, long? fromSequenceNumber)
    {
        await ReleaseLocksAsync();
        Load([], BrowseMode.Peek);
        await RunAsync(new($"Peeking {count:N0}", [new("Peek", count)], modal: false), async op =>
        {
            var step = Start(op, 0);
            await foreach (var page in service.PeekAsync(Source, count, fromSequenceNumber, op.Token))
                Append(page, step);
        });
    }

    /// <summary>Receives into the list as messages arrive; cancelling keeps what arrived (and its locks).</summary>
    public async Task ReceiveAsync(int count, bool deleteOnReceive)
    {
        await ReleaseLocksAsync();
        if (deleteOnReceive)
        {
            Load([], BrowseMode.Received);
            await RunAsync(new($"Receiving and deleting {count:N0}", [new("Receive", count)], modal: false), async op =>
            {
                var step = Start(op, 0);
                await foreach (var page in service.ReceiveAndDeleteAsync(Source, count, op.Token))
                    Append(page, step);
            });
            return;
        }

        Load([], BrowseMode.Locked);
        await RunAsync(new($"Receiving {count:N0} with PeekLock", [new("Receive", count)], modal: false), async op =>
        {
            var step = Start(op, 0);
            var locked = service.StartReceiving(Source, count, op.Token);
            _locked = locked;
            _ = RedrawOnLockLossAsync(locked);
            while (await locked.Held.WaitToReadAsync())
                Append(Drain(locked.Held).ToList(), step);
        });
    }

    private async Task RedrawOnLockLossAsync(ILockedMessages locked)
    {
        await foreach (var _ in locked.LockLost.ReadAllAsync())
            Changed?.Invoke();
    }

    private static IEnumerable<T> Drain<T>(ChannelReader<T> reader)
    {
        while (reader.TryRead(out var item)) yield return item;
    }

    private void Append(IReadOnlyCollection<MessageInfo> page, OperationStep step)
    {
        Messages = [.. Messages, .. page];
        step.Report(Messages.Count);
        Changed?.Invoke();
    }

    public Task CompleteAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Completing", "Mark complete", "Completed", targets, (l, m, ct) => l.CompleteAsync(m, ct));

    public Task AbandonAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Abandoning", "Abandon", "Abandoned", targets, (l, m, ct) => l.AbandonAsync(m, ct));

    public Task DeadLetterAsync(IReadOnlyList<MessageInfo> targets) =>
        SettleAsync("Dead-lettering", "Dead-letter", "Dead-lettered", targets, (l, m, ct) => l.DeadLetterAsync(m, DeadLetterReason, ct));

    /// <summary>
    /// Sends copies to the queue/topic in order and removes the originals, as a pipeline: messages are
    /// sent in chunks as soon as they are held, and completed as soon as their chunk is sent. Peeked
    /// dead-lettered messages are locked first by a scan (<see cref="IMessageService.StartLocking"/>) and
    /// sent in the order it finds them; received-and-deleted ones are only sent.
    /// </summary>
    public Task RetryAsync(IReadOnlyList<MessageInfo> targets)
    {
        var title = $"{(Source.IsDlq ? "Retrying" : "Requeuing")} {What(targets)}";
        var requeue = $"Requeue to {Source.SendTarget}";
        var n = targets.Count;
        return Mode switch
        {
            BrowseMode.Locked => RunAsync(new(title, [new(requeue, n), new("Mark complete", n)]),
                op => RetryHeldAsync(op, targets, _locked, "lock lost")),
            BrowseMode.Received => RunAsync(new(title, [new(requeue, n)]),
                op => RetryHeldAsync(op, targets, locked: null, "")),
            _ => RunAsync(new(title, [new("PeekLock", n), new(requeue, n), new("Mark complete", n)]),
                op => RetryPeekedAsync(op, targets)),
        };
    }

    private const int SendChunk = 100;
    private const int CompleteParallelism = 32;

    private Task RetryHeldAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets, ILockedMessages? locked, string notHeldReason)
    {
        var held = Channel.CreateUnbounded<MessageInfo>();
        foreach (var m in targets.OrderBy(m => m.SequenceNumber).Where(m => locked?.IsHeld(m.SequenceNumber) ?? true))
            held.Writer.TryWrite(m);
        held.Writer.Complete();
        return PipelineAsync(op, targets, held.Reader, locked, notHeldReason, lockStep: null);
    }

    private async Task RetryPeekedAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets)
    {
        // Scanning releases the messages it passes, which bumps their delivery count. Only the dead-letter
        // queue can afford that: in an active queue it would dead-letter unrelated messages.
        if (!Source.IsDlq) throw new InvalidOperationException("Retry of peeked messages works in the dead-letter queue only");
        var lockStep = op.Steps[0];
        lockStep.State = StepState.Running;
        Changed?.Invoke();
        await using var locked = service.StartLocking(Source, [.. targets.Select(m => m.SequenceNumber)], op.Token);
        await PipelineAsync(op, targets, locked.Held, locked, "not found in the dead-letter queue (gone, or locked elsewhere)", lockStep);
    }

    /// <summary>Send stage (chunks, in order, one at a time) feeding the complete stage (parallel).</summary>
    private async Task PipelineAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets, ChannelReader<MessageInfo> held, ILockedMessages? locked, string notHeldReason, OperationStep? lockStep)
    {
        var token = op.Token;
        var steps = op.Steps;
        var sendStep = steps[lockStep is null ? 0 : 1];
        var completeStep = locked is null ? null : steps[lockStep is null ? 1 : 2];
        var bySequence = targets.ToDictionary(m => m.SequenceNumber);
        var failures = new ConcurrentQueue<Failure>();
        var removed = new ConcurrentQueue<MessageInfo>();
        var seen = new HashSet<long>();
        var unseenReason = notHeldReason;
        var sent = Channel.CreateBounded<MessageInfo>(SendChunk * 2);
        if (lockStep is not null)
        {
            // The scan is done once its channel completes, even while the send stage is still busy.
            _ = held.Completion.ContinueWith(_ =>
            {
                lockStep.State = StepState.Done;
                Changed?.Invoke();
            }, TaskScheduler.Default);
        }

        async Task SendStageAsync()
        {
            var chunk = new List<MessageInfo>(SendChunk);
            try
            {
                while (await held.WaitToReadAsync())
                {
                    while (chunk.Count < SendChunk && held.TryRead(out var message))
                    {
                        seen.Add(message.SequenceNumber);
                        chunk.Add(bySequence[message.SequenceNumber]);
                    }
                    if (lockStep is not null) { lockStep.Report(seen.Count); Notify(lockStep); }
                    if (token.IsCancellationRequested)
                    {
                        // Stop taking new messages; held ones are released when the locks are disposed.
                        foreach (var m in chunk) failures.Enqueue(new(m, Failure.CancelledReason));
                        unseenReason = Failure.CancelledReason;
                        break;
                    }
                    await SendChunkAsync(chunk);
                    chunk.Clear();
                }
            }
            catch (Exception ex)
            {
                // The scan failed or was cancelled; whatever it did not lock is reported below.
                unseenReason = token.IsCancellationRequested ? Failure.CancelledReason : $"not locked: {Describe(ex)}";
            }
            finally
            {
                sent.Writer.TryComplete();
            }
        }

        async Task SendChunkAsync(List<MessageInfo> chunk)
        {
            sendStep.State = StepState.Running;
            var before = sendStep.Done;
            var count = 0;
            try
            {
                await foreach (var n in service.SendCopiesAsync(Source, chunk, token))
                {
                    count = n;
                    sendStep.Report(before + n);
                    Notify(sendStep);
                }
            }
            catch (Exception ex)
            {
                var reason = token.IsCancellationRequested ? Failure.CancelledReason : $"not sent: {Describe(ex)}";
                foreach (var m in chunk.Skip(count)) failures.Enqueue(new(m, reason));
            }
            foreach (var m in chunk.Take(count))
            {
                if (completeStep is null) removed.Enqueue(m);
                else await sent.Writer.WriteAsync(m);
            }
        }

        async Task CompleteStageAsync()
        {
            if (completeStep is null || locked is null) return;
            await Parallel.ForEachAsync(sent.Reader.ReadAllAsync(), new ParallelOptions { MaxDegreeOfParallelism = CompleteParallelism }, async (m, ct) =>
            {
                completeStep.State = StepState.Running;
                try
                {
                    await locked.CompleteAsync(m, ct);
                    removed.Enqueue(m);
                }
                catch (Exception ex)
                {
                    failures.Enqueue(new(m, $"sent, but original not completed: {Describe(ex)}"));
                }
                completeStep.Increment();
                Notify(completeStep);
            });
        }

        await Task.WhenAll(SendStageAsync(), CompleteStageAsync());

        foreach (var m in targets.Where(m => !seen.Contains(m.SequenceNumber)))
            failures.Enqueue(new(m, unseenReason));
        foreach (var step in steps) step.State = StepState.Done;
        Finish(op, "Retried", targets, [.. removed], [.. failures]);
    }

    /// <summary>Sends copies to the queue/topic and leaves the originals where they are.</summary>
    public Task ResendAsync(IReadOnlyList<MessageInfo> targets) =>
        RunAsync(new($"Sending {What(targets)}", [new($"Send to {Source.SendTarget}", targets.Count)]), async op =>
        {
            var (sent, failures) = await SendAsync(Start(op, 0), [.. targets.OrderBy(m => m.SequenceNumber)], op.Token);
            UpdateSelection(targets, failures.Select(f => f.Message));
            Report(op, "Sent", targets.Count, sent.Count, failures);
        });

    public async Task<int> PurgeAsync()
    {
        await ReleaseLocksAsync();
        var count = 0;
        await RunAsync(new($"Purging {Source.DisplayName}", [new("Purge", 0)]), async op =>
        {
            var step = Start(op, 0);
            try
            {
                await foreach (var n in service.PurgeAsync(Source, op.Token))
                {
                    count = n;
                    step.Report(n);
                    Notify(step);
                }
                op.Finish($"Purged {count:N0} message(s).", OperationOutcome.Succeeded);
            }
            catch (OperationCanceledException) when (op.Cancelled)
            {
                op.Finish($"Cancelled. Purged {count:N0} message(s).", OperationOutcome.Cancelled);
            }
            finally
            {
                // Also after a cancel: some of the listed messages may be gone.
                Load([], Mode);
            }
        });
        return count;
    }

    public async Task ReleaseLocksAsync()
    {
        if (_locked is null) return;
        var locked = _locked;
        _locked = null;
        await locked.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(ReleaseLocksAsync());

    private void Load(IReadOnlyList<MessageInfo> messages, BrowseMode mode)
    {
        Messages = messages;
        Mode = mode;
        Selected.Clear();
        Cursor = 0;
        Changed?.Invoke();
    }

    private async Task<(IReadOnlyList<MessageInfo> Sent, IReadOnlyList<Failure> Failures)> SendAsync(
        OperationStep step, IReadOnlyList<MessageInfo> messages, CancellationToken token)
    {
        step.Total = messages.Count;
        Notify(step);
        if (messages.Count == 0) return ([], []);
        var sent = 0;
        try
        {
            await foreach (var n in service.SendCopiesAsync(Source, messages, token))
            {
                sent = n;
                step.Report(n);
                Notify(step);
            }
            return ([.. messages], []);
        }
        catch (Exception ex)
        {
            var reason = ex is OperationCanceledException ? Failure.CancelledReason : $"not sent: {Describe(ex)}";
            return ([.. messages.Take(sent)], [.. messages.Skip(sent).Select(m => new Failure(m, reason))]);
        }
    }

    private Task SettleAsync(string verbing, string stepName, string pastVerb, IReadOnlyList<MessageInfo> targets, Func<ILockedMessages, MessageInfo, CancellationToken, Task> settle) =>
        RunAsync(new($"{verbing} {What(targets)}", [new(stepName, targets.Count)]), async op =>
        {
            var locked = _locked ?? throw new InvalidOperationException("No locked messages. Receive in PeekLock mode first.");
            var failures = new ConcurrentBag<Failure>();
            var done = await RunParallelAsync(Start(op, 0), targets, async (m, ct) =>
            {
                if (op.Cancelled) { failures.Add(new(m, Failure.CancelledReason)); return false; }
                try { await settle(locked, m, ct); return true; }
                catch (Exception ex) { failures.Add(new(m, Describe(ex))); return false; }
            });
            Finish(op, pastVerb, targets, done, [.. failures]);
        });

    private async Task<IReadOnlyList<MessageInfo>> RunParallelAsync(OperationStep step, IReadOnlyList<MessageInfo> items, Func<MessageInfo, CancellationToken, Task<bool>> op)
    {
        var ok = new ConcurrentBag<MessageInfo>();
        step.Total = items.Count;
        Notify(step);
        await Parallel.ForEachAsync(items, new ParallelOptions { MaxDegreeOfParallelism = 16 }, async (m, ct) =>
        {
            if (await op(m, ct)) ok.Add(m);
            step.Increment();
            Notify(step);
        });
        return [.. ok];
    }

    // A load clears its status line when done and rethrows errors. A batch operation keeps its dialog
    // open with the result, errors included, until Dismiss.
    private async Task RunAsync(OperationProgress operation, Func<OperationProgress, Task> body)
    {
        Operation = operation;
        Changed?.Invoke();
        try
        {
            await body(operation);
            if (operation.Modal && !operation.Finished) operation.Finish("Done.", OperationOutcome.Succeeded);
        }
        catch (OperationCanceledException) when (operation.Cancelled)
        {
            // Stopped on request; what was done so far stays.
            if (operation.Modal && !operation.Finished) operation.Finish("Cancelled.", OperationOutcome.Cancelled);
        }
        catch (Exception ex) when (operation.Modal)
        {
            operation.Finish(Describe(ex), OperationOutcome.Failed);
        }
        finally
        {
            if (!operation.Modal && Operation == operation) Operation = null;
            Changed?.Invoke();
        }
    }

    private OperationStep Start(OperationProgress op, int step)
    {
        var started = op.Start(step);
        Changed?.Invoke();
        return started;
    }

    private static string What(IReadOnlyList<MessageInfo> targets) =>
        targets is [var single] ? $"message #{single.SequenceNumber}" : $"{targets.Count} messages";

    private void Finish(OperationProgress op, string pastVerb, IReadOnlyList<MessageInfo> targets, IReadOnlyCollection<MessageInfo> removed, IReadOnlyCollection<Failure> failures)
    {
        var removedIds = removed.Select(m => m.SequenceNumber).ToHashSet();
        var focused = Focused;
        var remaining = Messages.Where(m => !removedIds.Contains(m.SequenceNumber)).ToList();
        Messages = remaining;
        Cursor = focused is not null && !removedIds.Contains(focused.SequenceNumber)
            ? remaining.IndexOf(focused)
            : Math.Min(Cursor, Math.Max(0, Messages.Count - 1));
        UpdateSelection(targets, failures.Select(f => f.Message));
        Report(op, pastVerb, targets.Count, removed.Count, failures);
    }

    private static void Report(OperationProgress op, string pastVerb, int total, int succeeded, IReadOnlyCollection<Failure> failures) =>
        op.Finish(Summary(pastVerb, total, succeeded, failures), failures switch
        {
            { Count: 0 } => OperationOutcome.Succeeded,
            _ when failures.All(f => f.IsCancelled) => OperationOutcome.Cancelled,
            _ => OperationOutcome.Failed,
        });

    // Acted-on messages leave the selection and failed ones are selected for a retry; the rest of the
    // selection stays, so acting on one message from its detail page keeps the others selected.
    private void UpdateSelection(IReadOnlyList<MessageInfo> targets, IEnumerable<MessageInfo> failed)
    {
        Selected.ExceptWith(targets.Select(m => m.SequenceNumber));
        Selected.UnionWith(failed.Select(m => m.SequenceNumber));
    }

    private static string Summary(string pastVerb, int total, int succeeded, IReadOnlyCollection<Failure> failures) => failures switch
    {
        { Count: 0 } => $"{pastVerb} {succeeded} of {total}.",
        _ when failures.All(f => f.IsCancelled) => $"Cancelled. {pastVerb} {succeeded} of {total}; the rest stay selected.",
        _ => $"{pastVerb} {succeeded} of {total}. Failed messages stay selected: {Examples(failures)}",
    };

    private static string Examples(IReadOnlyCollection<Failure> failures) =>
        string.Join("; ", failures.OrderBy(f => f.Message.SequenceNumber).Take(3).Select(f => $"#{f.Message.SequenceNumber} {f.Reason}"))
        + (failures.Count > 3 ? $" (+{failures.Count - 3} more)" : "");

    private static string Describe(Exception ex) => ex switch
    {
        OperationCanceledException => Failure.CancelledReason,
        MessageLockLostException => "lock lost",
        _ => ex.Message.Split('\n')[0],
    };

    private void Notify(OperationStep step)
    {
        if (step.Done == 0 || step.Done == step.Total || _progressThrottle.ShouldRender())
            Changed?.Invoke();
    }
}
