using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services.Caching;

/// <param name="directory">Cache root; defaults to the user's local application data folder.</param>
public class FileCache(ILogger<FileCache> logger, string? directory = null)
{
    private readonly string BaseDir = directory ?? Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "sb-console");

    public async Task<List<T>?> LoadAsync<T>(string key)
    {
        var path = GetPath(key);
        if (!File.Exists(path)) return null;

        try
        {
            var json = await File.ReadAllTextAsync(path);
            return JsonSerializer.Deserialize<List<T>>(json);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache read failed for {Key}", key);
            return null;
        }
    }

    public async Task SaveAsync<T>(string key, List<T> items)
    {
        var path = GetPath(key);
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            await File.WriteAllTextAsync(path, JsonSerializer.Serialize(items));
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Cache write failed for {Key}", key);
        }
    }

    public string CacheDirectory => BaseDir;

    /// <returns>Number of cache files removed.</returns>
    public int Clear()
    {
        if (!Directory.Exists(BaseDir)) return 0;
        var count = Directory.EnumerateFiles(BaseDir, "*", SearchOption.AllDirectories).Count();
        Directory.Delete(BaseDir, true);
        logger.LogInformation("Cache cleared: {Count} file(s)", count);
        return count;
    }

    private string GetPath(string key) => Path.Combine(BaseDir, key);
}
