using System.Runtime.CompilerServices;
using System.Threading.Channels;

namespace ServiceBusConsole.Tests;

public sealed class FakeNamespaceService : INamespaceService
{
    public List<NamespaceInfo> Namespaces { get; } = [];
    public List<AzureSubscription> Subscriptions { get; } = [];
    public Exception? SubscriptionsError { get; set; }

    /// <summary>When set, the listing waits on it after the last subscription, like a slow live refresh.</summary>
    public TaskCompletionSource? HoldListing { get; set; }

    /// <summary>When set, namespace listings wait on it after the last namespace, like a slow live refresh.</summary>
    public TaskCompletionSource? HoldNamespaces { get; set; }

    /// <summary>The Azure CLI's current subscription; null like a machine without a profile.</summary>
    public string? CurrentSubscriptionId { get; set; }

    public Task<string?> GetCurrentSubscriptionIdAsync(CancellationToken ct = default) => Task.FromResult(CurrentSubscriptionId);

    /// <summary>Subscription IDs that report no Service Bus namespaces.</summary>
    public HashSet<string> WithoutNamespaces { get; } = [];

    public async IAsyncEnumerable<bool> HasNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        yield return !WithoutNamespaces.Contains(subscriptionId);
    }

    public async IAsyncEnumerable<AzureSubscription> GetSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        if (SubscriptionsError is not null) throw SubscriptionsError;
        foreach (var sub in Subscriptions)
        {
            await Task.Yield();
            yield return sub;
        }
        if (HoldListing is { } hold) await hold.Task.WaitAsync(ct);
    }

    public async IAsyncEnumerable<NamespaceInfo> GetNamespacesAsync(string subscriptionId, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var ns in Namespaces)
        {
            await Task.Yield();
            yield return ns;
        }
        if (HoldNamespaces is { } hold) await hold.Task.WaitAsync(ct);
    }
}

public sealed class FakeMetricsService : IMetricsService
{
    /// <summary>Every scope loaded, in order.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<MetricsScope> Requested { get; } = [];

    /// <summary>The window of each load, in order.</summary>
    public System.Collections.Concurrent.ConcurrentQueue<MetricsWindow> Windows { get; } = [];
    public Exception? LoadError { get; set; }

    public async IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Requested.Enqueue(scope);
        Windows.Enqueue(window);
        await Task.Yield();
        if (LoadError is not null) throw LoadError;
        yield return Sample(window, scope.Match(_ => 12, e => 1 + e.Name.Length % 4));
    }

    /// <summary>
    /// Traffic over 96 steps, whatever the window's intervals: a busy daytime, a backlog that builds while consumers lag,
    /// then drains, and a few dead letters.
    /// </summary>
    public static MessageMetrics Sample(MetricsWindow window, double scale)
    {
        var starts = window.IntervalStarts().ToList();
        List<MetricPoint> Series(Func<int, double> value) =>
            [.. starts.Select((t, i) => new MetricPoint(t, Math.Round(Math.Max(0, value(i * 96 / starts.Count)) * scale)))];
        static double Day(int i) => 20 + 140 * Math.Max(0, Math.Sin(Math.PI * (i - 20) / 56));
        return new()
        {
            Window = window,
            Incoming = Series(i => Day(i) + (i is >= 52 and < 58 ? 90 : 0)),
            Outgoing = Series(i => i is >= 52 and < 64 ? Day(i) * 0.6 : Day(i - 1)),
            Active = Series(i => i switch { < 52 => 4 + i % 3, < 58 => 60 * (i - 51), < 70 => 360 - 30 * (i - 57), _ => 6 }),
            DeadLettered = Series(i => i < 55 ? 8 : i < 80 ? 40 : 65),
        };
    }
}

public sealed class FakeQueueService : IQueueService
{
    public List<QueueInfo> Queues { get; } = [];
    public List<string> Deleted { get; } = [];
    public Exception? LoadError { get; set; }

    public int Loads { get; private set; }

    /// <summary>When set, listings wait on it before the first queue, like a slow namespace.</summary>
    public TaskCompletionSource? HoldLoads { get; set; }

    /// <summary>Changes a queue while a page may be polling.</summary>
    public void Update(string name, Func<QueueInfo, QueueInfo> change)
    {
        lock (Queues) Queues[Queues.FindIndex(q => q.Name == name)] = change(Queues.Single(q => q.Name == name));
    }

    public void Add(QueueInfo queue)
    {
        lock (Queues) Queues.Add(queue);
    }

    public async IAsyncEnumerable<QueueInfo> GetQueuesAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        Loads++;
        await Task.Yield();
        if (HoldLoads is { } hold) await hold.Task.WaitAsync(ct);
        if (LoadError is not null) throw LoadError;
        List<QueueInfo> queues;
        lock (Queues) queues = [.. Queues];
        foreach (var q in queues) yield return q;
    }

    public void Delete(string queueName)
    {
        Deleted.Add(queueName);
        lock (Queues) Queues.RemoveAll(q => q.Name == queueName);
    }
}

public sealed class FakeTopicService : ITopicService
{
    public List<TopicInfo> Topics { get; } = [];
    public List<SubscriptionInfo> Subscriptions { get; } = [];

    /// <summary>Changes the topic list while a page may be polling.</summary>
    public void Change(Action<List<TopicInfo>> change)
    {
        lock (Topics) change(Topics);
    }

    public async IAsyncEnumerable<TopicInfo> GetTopicsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        List<TopicInfo> topics;
        lock (Topics) topics = [.. Topics];
        foreach (var t in topics) yield return t;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetSubscriptionsAsync(string topicName, [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.Where(s => s.TopicName == topicName).ToList()) yield return s;
    }

    public async IAsyncEnumerable<SubscriptionInfo> GetAllSubscriptionsAsync([EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.Yield();
        foreach (var s in Subscriptions.ToList()) yield return s;
    }
}

public sealed class FakeMessageService : IMessageService
{
    public List<MessageInfo> Messages { get; } = [];
    public List<MessageSource> Purged { get; } = [];
    public List<MessageSource> Peeked { get; } = [];

    private const int PageSize = 100;

    /// <summary>When set, peeking pauses after the first page until it completes.</summary>
    public TaskCompletionSource? PausePeek { get; set; }

    /// <summary>When set, peeking throws it.</summary>
    public Exception? PeekError { get; set; }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> PeekAsync(MessageSource source, int count, long? fromSequenceNumber = null, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Peeked.Add(source);
        if (PeekError is { } error) throw error;
        var pages = Messages.Where(m => fromSequenceNumber is null || m.SequenceNumber >= fromSequenceNumber).Take(count).Chunk(PageSize);
        var first = true;
        foreach (var page in pages)
        {
            if (!first && PausePeek is { } pause) await pause.Task.WaitAsync(ct);
            first = false;
            await Task.Yield();
            yield return page;
        }
    }

    public async IAsyncEnumerable<IReadOnlyList<MessageInfo>> ReceiveAndDeleteAsync(MessageSource source, int count, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var page in Messages.Take(count).Chunk(PageSize))
        {
            await Task.Yield();
            yield return [.. page.Select(m => m with { State = MessageState.Received })];
        }
    }

    // Completes run in parallel, so recording must be thread-safe.
    public System.Collections.Concurrent.ConcurrentQueue<long> Completed { get; } = [];

    /// <summary>When set, the lock scan stops after <see cref="PauseScanAfter"/> messages until it completes.</summary>
    public TaskCompletionSource? PauseScan { get; set; }
    public int PauseScanAfter { get; set; }

    /// <summary>Sequence numbers someone else holds a lock on: the lock scan cannot get them.</summary>
    public HashSet<long> LockedElsewhere { get; } = [];

    public ILockedMessages StartReceiving(MessageSource source, int count, CancellationToken ct = default)
    {
        var batch = Messages.Take(count).Select(m => m with { State = MessageState.Locked }).ToList();
        return new FakeLockedMessages(this, batch, batch.ToAsyncEnumerable());
    }

    public ILockedMessages StartLocking(MessageSource source, IReadOnlyCollection<long> sequenceNumbers, CancellationToken ct = default)
    {
        var lockable = Messages.Where(m => sequenceNumbers.Contains(m.SequenceNumber) && !LockedElsewhere.Contains(m.SequenceNumber))
            .Select(m => m with { State = MessageState.Locked }).ToList();
        return new FakeLockedMessages(this, lockable, ScanAsync(lockable, ct));
    }

    private async IAsyncEnumerable<MessageInfo> ScanAsync(IReadOnlyList<MessageInfo> lockable, [EnumeratorCancellation] CancellationToken ct)
    {
        foreach (var (i, m) in lockable.Index())
        {
            if (i == PauseScanAfter && PauseScan is { } pause) await pause.Task.WaitAsync(ct);
            yield return m;
        }
    }

    public List<(MessageSource Source, long SequenceNumber)> Sent { get; } = [];

    /// <summary>When set, sending waits on it, so a test can see an operation in progress.</summary>
    public TaskCompletionSource? HoldSend { get; set; }

    public async IAsyncEnumerable<int> SendCopiesAsync(MessageSource source, IReadOnlyList<MessageInfo> messages, [EnumeratorCancellation] CancellationToken ct = default)
    {
        if (HoldSend is { } hold) await hold.Task.WaitAsync(ct);
        Sent.AddRange(messages.Select(m => (source, m.SequenceNumber)));
        yield return messages.Count;
    }

    public async IAsyncEnumerable<int> PurgeAsync(MessageSource source, [EnumeratorCancellation] CancellationToken ct = default)
    {
        Purged.Add(source);
        await Task.Yield();
        yield return Messages.Count;
    }
}

/// <summary>Holds <paramref name="messages"/> from the start; <paramref name="locking"/> is what arrives on <see cref="Held"/>.</summary>
public sealed class FakeLockedMessages(FakeMessageService service, IEnumerable<MessageInfo> messages, IAsyncEnumerable<MessageInfo> locking) : ILockedMessages
{
    private readonly HashSet<long> _held = [.. messages.Select(m => m.SequenceNumber)];
    private readonly Channel<long> _lockLost = Channel.CreateUnbounded<long>();

    public ChannelReader<MessageInfo> Held { get; } = Pump(locking);

    public ChannelReader<long> LockLost => _lockLost.Reader;

    public bool Disposed { get; private set; }

    public bool IsHeld(long sequenceNumber)
    {
        lock (_held) return _held.Contains(sequenceNumber);
    }

    public Task CompleteAsync(MessageInfo message, CancellationToken ct = default)
    {
        Settle(message);
        service.Completed.Enqueue(message.SequenceNumber);
        return Task.CompletedTask;
    }

    public Task AbandonAsync(MessageInfo message, CancellationToken ct = default)
    {
        Settle(message);
        return Task.CompletedTask;
    }

    public Task DeadLetterAsync(MessageInfo message, string reason, CancellationToken ct = default)
    {
        Settle(message);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        _lockLost.Writer.TryComplete();
        return ValueTask.CompletedTask;
    }

    private static ChannelReader<MessageInfo> Pump(IAsyncEnumerable<MessageInfo> locking)
    {
        var channel = Channel.CreateUnbounded<MessageInfo>();
        _ = Task.Run(async () =>
        {
            try
            {
                await foreach (var m in locking) await channel.Writer.WriteAsync(m);
                channel.Writer.TryComplete();
            }
            catch (Exception ex)
            {
                channel.Writer.TryComplete(ex);
            }
        });
        return channel.Reader;
    }

    private void Settle(MessageInfo message)
    {
        bool removed;
        lock (_held) removed = _held.Remove(message.SequenceNumber);
        if (!removed)
            throw new InvalidOperationException($"Message #{message.SequenceNumber} is no longer locked");
    }
}

/// <summary>Moves a chunk like a transaction: all of it, or none when <see cref="Fails"/> matches it.</summary>
public sealed class FakeTransactionService : ITransactionService
{
    public int MaxMessages { get; set; } = 100;

    /// <summary>Committed chunks, in order.</summary>
    public List<IReadOnlyList<long>> Moved { get; } = [];

    /// <summary>Chunks that rolled back.</summary>
    public List<IReadOnlyList<long>> RolledBack { get; } = [];

    public Func<IReadOnlyList<MessageInfo>, bool> Fails { get; set; } = _ => false;

    /// <summary>When set, moving waits on it, so a test can see an operation in progress.</summary>
    public TaskCompletionSource? Hold { get; set; }

    public async Task MoveAsync(MessageSource source, ILockedMessages locked, IReadOnlyList<MessageInfo> messages, CancellationToken ct = default)
    {
        await Task.Yield();
        if (Hold is { } hold) await hold.Task.WaitAsync(ct);
        if (messages.Count > MaxMessages) throw new ArgumentException($"{messages.Count} messages in one transaction");
        if (Fails(messages))
        {
            lock (RolledBack) RolledBack.Add([.. messages.Select(m => m.SequenceNumber)]);
            throw new InvalidOperationException("transaction rolled back");
        }
        foreach (var m in messages) await locked.CompleteAsync(m, ct);
        lock (Moved) Moved.Add([.. messages.Select(m => m.SequenceNumber)]);
    }
}

public sealed class FakeDeletionService(FakeQueueService queues) : IDeletionService
{
    public Task DeleteQueueAsync(string queueName, CancellationToken ct = default)
    {
        queues.Delete(queueName);
        return Task.CompletedTask;
    }

    public Task DeleteTopicAsync(string topicName, CancellationToken ct = default) => Task.CompletedTask;

    public Task DeleteSubscriptionAsync(string topicName, string subscriptionName, CancellationToken ct = default) => Task.CompletedTask;
}

/// <summary>The fake services of a test; every namespace it opens shares them.</summary>
public sealed class FakeServices
{
    public FakeServices() => Deletion = new(Queues);

    public FakeQueueService Queues { get; } = new();
    public FakeTopicService Topics { get; } = new();
    public FakeMessageService Messages { get; init; } = new();
    public FakeMetricsService Metrics { get; } = new();
    public FakeDeletionService Deletion { get; }
    public FakeTransactionService Transactions { get; } = new();
}

public abstract class FakeNamespace(NamespaceInfo info, FakeServices services) : IServiceBusNamespace
{
    protected FakeServices Services => services;

    public NamespaceInfo Info => info;
    public IQueueService Queues => services.Queues;
    public IMessageService Messages => services.Messages;
    public bool Disposed { get; private set; }

    /// <summary>Like the real opener: the emulator, Basic by its SKU, any other namespace Standard.</summary>
    public static FakeNamespace For(NamespaceInfo info, FakeServices services) => info switch
    {
        _ when info == NamespaceInfo.Emulator => new FakeEmulatorNamespace(services),
        { Sku: "Basic" } => new FakeBasicNamespace(info, services),
        _ => new FakeStandardNamespace(info, services),
    };

    public ValueTask DisposeAsync()
    {
        Disposed = true;
        return ValueTask.CompletedTask;
    }
}

/// <summary>Like the local emulator: topics, but no metrics, deletion or transactions.</summary>
public sealed class FakeEmulatorNamespace(FakeServices services) : FakeNamespace(NamespaceInfo.Emulator, services), IHasTopics
{
    public ITopicService Topics => Services.Topics;
}

/// <summary>Like the Basic tier: metrics and deletion, no topics or transactions.</summary>
public sealed class FakeBasicNamespace(NamespaceInfo info, FakeServices services) : FakeNamespace(info, services), IHasMetrics, IHasDeletion
{
    public IMetricsService Metrics => Services.Metrics;
    public IDeletionService Deletion => Services.Deletion;
}

/// <summary>Like the Standard and Premium tiers: every feature.</summary>
public sealed class FakeStandardNamespace(NamespaceInfo info, FakeServices services)
    : FakeNamespace(info, services), IHasTopics, IHasMetrics, IHasDeletion, IHasTransactions
{
    public ITopicService Topics => Services.Topics;
    public IMetricsService Metrics => Services.Metrics;
    public IDeletionService Deletion => Services.Deletion;
    public ITransactionService Transactions => Services.Transactions;
}

public sealed class FakeNamespaceOpener(Func<NamespaceInfo, IServiceBusNamespace> open) : INamespaceOpener
{
    public IServiceBusNamespace Open(NamespaceInfo info) => open(info);
}
