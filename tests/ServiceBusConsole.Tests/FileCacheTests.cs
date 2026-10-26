using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Tests;

public sealed class FileCacheTests
{
    [Fact]
    public void RegisteredLikeTheApp_UsesTheUserCacheFolder()
    {
        using var services = new ServiceCollection().AddLogging().AddSingleton<FileCache>().BuildServiceProvider();

        var cache = services.GetRequiredService<FileCache>();

        cache.CacheDirectory.ShouldBe(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sb-console"));
    }
}
