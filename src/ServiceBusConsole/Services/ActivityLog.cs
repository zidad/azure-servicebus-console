namespace ServiceBusConsole.Services;

/// <summary>What happened in this session, newest last; keeps the last <see cref="Capacity"/> entries. Thread-safe.</summary>
public sealed class ActivityLog(TimeProvider time, int capacity = ActivityLog.DefaultCapacity)
{
    public const int DefaultCapacity = 200;

    private readonly Queue<ActivityEntry> _entries = new();
    private readonly Lock _lock = new();

    public ActivityLog() : this(TimeProvider.System) { }

    public int Capacity { get; } = capacity > 0 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));

    public TimeProvider Time { get; } = time;

    public IReadOnlyList<ActivityEntry> Entries
    {
        get { lock (_lock) return [.. _entries]; }
    }

    public ActivityEntry Record(ActivityKind kind, string text, MessageSource? source = null)
    {
        var entry = new ActivityEntry(Time.GetUtcNow(), kind, text, source);
        lock (_lock)
        {
            _entries.Enqueue(entry);
            while (_entries.Count > Capacity) _entries.Dequeue();
        }
        return entry;
    }
}
