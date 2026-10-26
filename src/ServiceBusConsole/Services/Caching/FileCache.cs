using System.Runtime.CompilerServices;
using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services.Caching;

/// <param name="directory">Cache root; defaults to the user's local application data folder.</param>
public sealed class FileCache(ILogger<FileCache> logger, string? directory = null)
{
    public string CacheDirectory { get; } = directory
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sb-console");

    public async Task<IReadOnlyList<T>?> LoadAsync<T>(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return null;
        try
        {
            return JsonSerializer.Deserialize<List<T>>(await File.ReadAllTextAsync(path));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}", key);
            return null;
        }
    }

    public async Task SaveAsync<T>(string key, IEnumerable<T> items)
    {
        var path = GetPath(key);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path) ?? CacheDirectory);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(items));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    /// <summary>
    /// The cached items (marked by <paramref name="asCached"/>), then the live ones as they arrive;
    /// a complete live listing replaces the cache. <paramref name="live"/> is only called after the
    /// cached items, so a live listing that fails at once still leaves them shown.
    /// </summary>
    public async IAsyncEnumerable<T> CachedThenLiveAsync<T>(
        string key, Func<IAsyncEnumerable<T>> live, Func<T, T> asCached, [EnumeratorCancellation] CancellationToken ct = default)
    {
        foreach (var item in await LoadAsync<T>(key) ?? [])
        {
            if (ct.IsCancellationRequested) yield break;
            yield return asCached(item);
        }

        var fresh = new List<T>();
        await foreach (var item in live().WithCancellation(ct))
        {
            fresh.Add(item);
            yield return item;
        }

        if (!ct.IsCancellationRequested && fresh.Count > 0)
            _ = SaveAsync(key, fresh);
    }

    /// <summary>Drops matching items from a cached listing, e.g. after deleting the entity.</summary>
    public async Task RemoveAsync<T>(string key, Func<T, bool> match)
    {
        if (await LoadAsync<T>(key) is { } cached)
            await SaveAsync(key, cached.Where(item => !match(item)));
    }

    /// <returns>Number of cache files removed.</returns>
    public int Clear()
    {
        if (!Directory.Exists(CacheDirectory)) return 0;
        var count = Directory.EnumerateFiles(CacheDirectory, "*", SearchOption.AllDirectories).Count();
        Directory.Delete(CacheDirectory, recursive: true);
        logger.LogInformation("Cache cleared: {Count} file(s)", count);
        return count;
    }

    private string GetPath(string key) => Path.Combine(CacheDirectory, key);
}
