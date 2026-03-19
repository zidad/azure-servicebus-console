using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class FileCache(ILogger<FileCache> logger)
{
    private static readonly string BaseDir = Path.Combine(
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

    private static string GetPath(string key) => Path.Combine(BaseDir, key);
}
