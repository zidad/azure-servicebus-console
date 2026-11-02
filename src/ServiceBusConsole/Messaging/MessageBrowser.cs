using System.Collections.Concurrent;
using System.Threading.Channels;

namespace ServiceBusConsole.Messaging;

/// <summary>
/// The message list of one queue or subscription (active or DLQ), its selection, and the
/// receiver that holds any PeekLocked messages. Outlives the page so the message detail page
/// can act on the same locks. List mutations happen on the caller's (dispatcher) thread;
/// <see cref="Changed"/> may fire from background threads.
/// </summary>
public sealed class MessageBrowser(MessageSource source, IServiceBusNamespace ns) : IAsyncDisposable
{
    private const string DeadLetterReason = "Dead-lettered manually";
    private readonly IMessageService _service = ns.Messages;
    private ILockedMessages? _locked;
    private readonly RenderThrottle _progressThrottle = new(100);

    /// <summary>The namespace this browser reads and its locks belong to.</summary>
    public IServiceBusNamespace Namespace { get; } = ns;
    public MessageSource Source { get; } = source;
    public BrowseMode Mode { get; private set; } = BrowseMode.Peek;
    public IReadOnlyList<MessageInfo> Messages { get; private set; } = [];
    public HashSet<long> Selected { get; } = [];
    public int Cursor { get; set; }
    /// <summary>The batch operation in progress or finished (until <see cref="Dismiss"/>), for the progress dialog; null when idle.</summary>
    public OperationProgress? Operation { get; private set; }

    public event Action? Changed;

    /// <summary>A load or batch operation ended without throwing: finished with a result, or (a load) done or cancelled.</summary>
    public event Action<OperationProgress>? Ended;

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
            await foreach (var page in _service.PeekAsync(Source, count, fromSequenceNumber, op.Token))
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
                await foreach (var page in _service.ReceiveAndDeleteAsync(Source, count, op.Token))
                    Append(page, step);
            });
            return;
        }

        Load([], BrowseMode.Locked);
        await RunAsync(new($"Receiving {count:N0} with PeekLock", [new("Receive", count)], modal: false), async op =>
        {
            var step = Start(op, 0);
            var locked = _service.StartReceiving(Source, count, op.Token);
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
    /// Sends copies to the queue/topic in order and removes the originals, as a pipeline: messages go in chunks as soon as
    /// they are held. With transactions (<see cref="IHasTransactions"/>) each chunk moves in one transaction, sent and
    /// completed together or not at all; without, a chunk's originals are completed once it is sent (at least once). Peeked
    /// dead-lettered messages are locked first by a scan (<see cref="IMessageService.StartLocking"/>) and sent in the order
    /// it finds them; received-and-deleted ones are only sent.
    /// </summary>
    public Task RetryAsync(IReadOnlyList<MessageInfo> targets)
    {
        var title = $"{(Source.IsDlq ? "Retrying" : "Requeuing")} {What(targets)}";
        var requeue = $"Requeue to {Source.SendTarget}";
        var move = $"Move to {Source.SendTarget}";
        var n = targets.Count;
        return (Mode, Namespace) switch
        {
            (BrowseMode.Received, _) => RunAsync(new(title, [new(requeue, n)]),
                op => RetryHeldAsync(op, targets, locked: null, "")),
            (BrowseMode.Locked, IHasTransactions { Transactions: var tx }) => RunAsync(new(title, [new(move, n)]),
                op => MoveHeldAsync(op, targets, tx)),
            (BrowseMode.Locked, _) => RunAsync(new(title, [new(requeue, n), new("Mark complete", n)]),
                op => RetryHeldAsync(op, targets, _locked, "lock lost")),
            (_, IHasTransactions { Transactions: var tx }) => RunAsync(new(title, [new("PeekLock", n), new(move, n)]),
                op => LockFirstAsync(op, targets, (run, locked, lockStep) => MoveAsync(op, run, tx, locked, lockStep))),
            _ => RunAsync(new(title, [new("PeekLock", n), new(requeue, n), new("Mark complete", n)]),
                op => LockFirstAsync(op, targets, (run, locked, lockStep) => PipelineAsync(op, run, locked, lockStep))),
        };
    }

    private const int SendChunk = 100;
    private const int CompleteParallelism = 32;

    private Task RetryHeldAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets, ILockedMessages? locked, string notHeldReason) =>
        PipelineAsync(op, new RetryRun(targets, HeldInOrder(targets, locked), lockStep: null, notHeldReason, Notify, op.Token), locked, lockStep: null);

    private Task MoveHeldAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets, ITransactionService tx)
    {
        var locked = _locked ?? throw new InvalidOperationException("No locked messages. Receive in PeekLock mode first.");
        return MoveAsync(op, new RetryRun(targets, HeldInOrder(targets, locked), lockStep: null, "lock lost", Notify, op.Token), tx, locked, lockStep: null);
    }

    // Locked ones only while still held.
    private static ChannelReader<MessageInfo> HeldInOrder(IReadOnlyList<MessageInfo> targets, ILockedMessages? locked)
    {
        var held = Channel.CreateUnbounded<MessageInfo>();
        foreach (var m in targets.OrderBy(m => m.SequenceNumber).Where(m => locked?.IsHeld(m.SequenceNumber) ?? true))
            held.Writer.TryWrite(m);
        held.Writer.Complete();
        return held.Reader;
    }

    private async Task LockFirstAsync(OperationProgress op, IReadOnlyList<MessageInfo> targets, Func<RetryRun, ILockedMessages, OperationStep, Task> pipeline)
    {
        // Scanning releases the messages it passes, which bumps their delivery count. Only the dead-letter
        // queue can afford that: in an active queue it would dead-letter unrelated messages.
        if (!Source.IsDlq) throw new InvalidOperationException("Retry of peeked messages works in the dead-letter queue only");
        var lockStep = op.Steps[0];
        lockStep.State = StepState.Running;
        Changed?.Invoke();
        await using var locked = _service.StartLocking(Source, [.. targets.Select(m => m.SequenceNumber)], op.Token);
        // The scan is done once its channel completes, even while later steps are still busy.
        _ = locked.Held.Completion.ContinueWith(_ =>
        {
            lockStep.State = StepState.Done;
            Changed?.Invoke();
        }, TaskScheduler.Default);
        var run = new RetryRun(targets, locked.Held, lockStep, "not found in the dead-letter queue (gone, or locked elsewhere)", Notify, op.Token);
        await pipeline(run, locked, lockStep);
    }

    /// <summary>Send stage (chunks, in order, one at a time) feeding the complete stage (parallel).</summary>
    private async Task PipelineAsync(OperationProgress op, RetryRun run, ILockedMessages? locked, OperationStep? lockStep)
    {
        var token = op.Token;
        var sendStep = op.Steps[lockStep is null ? 0 : 1];
        var completeStep = locked is null ? null : op.Steps[lockStep is null ? 1 : 2];
        var sent = Channel.CreateBounded<MessageInfo>(SendChunk * 2);

        async Task SendStageAsync()
        {
            try
            {
                await run.ChunksAsync(SendChunk, SendChunkAsync);
            }
            finally
            {
                sent.Writer.TryComplete();
            }
        }

        async Task SendChunkAsync(IReadOnlyList<MessageInfo> chunk)
        {
            sendStep.State = StepState.Running;
            var before = sendStep.Done;
            var count = 0;
            try
            {
                await foreach (var n in _service.SendCopiesAsync(Source, chunk, token))
                {
                    count = n;
                    sendStep.Report(before + n);
                    Notify(sendStep);
                }
            }
            catch (Exception ex)
            {
                run.Fail(chunk.Skip(count), token.IsCancellationRequested ? Failure.CancelledReason : $"not sent: {Describe(ex)}");
            }
            foreach (var m in chunk.Take(count))
            {
                if (completeStep is null) run.Removed.Enqueue(m);
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
                    run.Removed.Enqueue(m);
                }
                catch (Exception ex)
                {
                    run.Fail([m], $"sent, but original not completed: {Describe(ex)}");
                }
                completeStep.Increment();
                Notify(completeStep);
            });
        }

        await Task.WhenAll(SendStageAsync(), CompleteStageAsync());
        Conclude(op, run);
    }

    /// <summary>Chunks, in order, one at a time, each in one transaction; a failed chunk stays where it was, still held.</summary>
    private async Task MoveAsync(OperationProgress op, RetryRun run, ITransactionService tx, ILockedMessages locked, OperationStep? lockStep)
    {
        var token = op.Token;
        var moveStep = op.Steps[lockStep is null ? 0 : 1];
        await run.ChunksAsync(Math.Min(SendChunk, tx.MaxMessages), async chunk =>
        {
            moveStep.State = StepState.Running;
            try
            {
                await tx.MoveAsync(Source, locked, chunk, token);
                foreach (var m in chunk) run.Removed.Enqueue(m);
                moveStep.Report(moveStep.Done + chunk.Count);
                Notify(moveStep);
            }
            catch (Exception ex)
            {
                run.Fail(chunk, token.IsCancellationRequested ? Failure.CancelledReason : $"not moved: {Describe(ex)}");
            }
        });
        Conclude(op, run);
    }

    private void Conclude(OperationProgress op, RetryRun run)
    {
        run.FailUnseen();
        foreach (var step in op.Steps) step.State = StepState.Done;
        Finish(op, "Retried", run.Targets, [.. run.Removed], [.. run.Failures]);
    }

    /// <summary>What a retry pipeline has seen held, removed and failed.</summary>
    private sealed class RetryRun(
        IReadOnlyList<MessageInfo> targets, ChannelReader<MessageInfo> held, OperationStep? lockStep, string notHeldReason,
        Action<OperationStep> notify, CancellationToken token)
    {
        private readonly Dictionary<long, MessageInfo> _bySequence = targets.ToDictionary(m => m.SequenceNumber);
        private readonly HashSet<long> _seen = [];
        private string _unseenReason = notHeldReason;

        public IReadOnlyList<MessageInfo> Targets => targets;
        public ConcurrentQueue<MessageInfo> Removed { get; } = [];
        public ConcurrentQueue<Failure> Failures { get; } = [];

        /// <summary>Hands held messages to <paramref name="handle"/> in order, in chunks of up to <paramref name="size"/>, each as soon as there are any.</summary>
        public async Task ChunksAsync(int size, Func<IReadOnlyList<MessageInfo>, Task> handle)
        {
            try
            {
                while (await held.WaitToReadAsync())
                {
                    var chunk = new List<MessageInfo>(size);
                    while (chunk.Count < size && held.TryRead(out var message))
                    {
                        _seen.Add(message.SequenceNumber);
                        chunk.Add(_bySequence[message.SequenceNumber]);
                    }
                    if (lockStep is not null) { lockStep.Report(_seen.Count); notify(lockStep); }
                    if (token.IsCancellationRequested)
                    {
                        // Stop taking new messages; held ones are released when the locks are disposed.
                        Fail(chunk, Failure.CancelledReason);
                        _unseenReason = Failure.CancelledReason;
                        return;
                    }
                    await handle(chunk);
                }
            }
            catch (Exception ex)
            {
                // The scan failed or was cancelled; whatever it did not lock is failed by FailUnseen.
                _unseenReason = token.IsCancellationRequested ? Failure.CancelledReason : $"not locked: {Describe(ex)}";
            }
        }

        public void Fail(IEnumerable<MessageInfo> messages, string reason)
        {
            foreach (var m in messages) Failures.Enqueue(new(m, reason));
        }

        public void FailUnseen() => Fail(targets.Where(m => !_seen.Contains(m.SequenceNumber)), _unseenReason);
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
                await foreach (var n in _service.PurgeAsync(Source, op.Token))
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
            await foreach (var n in _service.SendCopiesAsync(Source, messages, token))
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
        Ended?.Invoke(operation);
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
        _ when ErrorText.IsFirewall(ex) => "blocked by the namespace firewall",
        _ => ex.Message.Split('\n')[0],
    };

    private void Notify(OperationStep step)
    {
        if (step.Done == 0 || step.Done == step.Total || _progressThrottle.ShouldRender())
            Changed?.Invoke();
    }
}
