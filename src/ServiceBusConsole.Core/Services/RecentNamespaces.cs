namespace ServiceBusConsole.Core.Services;

/// <summary>The last namespaces the user opened, newest first, kept in the file cache. Changed is raised on any thread.</summary>
public sealed class RecentNamespaces(FileCache cache, TimeProvider time)
{
    public const int Capacity = 5;
    private const string CacheKey = "recent-namespaces.json";

    private readonly Lock _sync = new();
    private readonly SemaphoreSlim _saving = new(1, 1);
    private Task? _load;

    public RecentNamespaces(FileCache cache) : this(cache, TimeProvider.System) { }

    public TimeProvider Time { get; } = time;

    /// <summary>Newest first; empty until loaded.</summary>
    public IReadOnlyList<RecentNamespace> Items { get; private set; } = [];

    public event Action? Changed;

    // Off the caller's (the renderer's) context, like the subscription catalog.
    public Task EnsureLoadedAsync()
    {
        lock (_sync) return _load ??= Task.Run(LoadAsync);
    }

    private async Task LoadAsync()
    {
        var cached = await cache.LoadAsync<RecentNamespace>(CacheKey) ?? [];
        lock (_sync) Items = [.. cached.Take(Capacity)];
        Changed?.Invoke();
    }

    public DateTimeOffset? OpenedAt(AzureSubscription subscription, NamespaceInfo ns) =>
        Items.FirstOrDefault(r => r.Is(subscription, ns))?.Opened;

    /// <summary>Puts <paramref name="ns"/> first, opened now, and saves the list.</summary>
    public async Task RecordAsync(AzureSubscription subscription, NamespaceInfo ns)
    {
        await EnsureLoadedAsync();
        lock (_sync)
            Items = [new(subscription, ns, Time.GetUtcNow()), .. Items.Where(r => !r.Is(subscription, ns)).Take(Capacity - 1)];
        Changed?.Invoke();
        // One save at a time, each of the list as it is then: the last one written is the newest.
        await _saving.WaitAsync();
        try
        {
            await cache.SaveAsync(CacheKey, Items);
        }
        finally
        {
            _saving.Release();
        }
    }
}
