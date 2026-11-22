using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

public sealed class FileCacheTests
{
    [Test]
    public void RegisteredLikeTheApp_UsesTheUserCacheFolder()
    {
        using var services = new ServiceCollection().AddLogging().AddSingleton<FileCache>().BuildServiceProvider();

        var cache = services.GetRequiredService<FileCache>();

        cache.CacheDirectory.ShouldBe(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "sb-console"));
    }

    private static FileCache NewCache() =>
        new(NullLogger<FileCache>.Instance, Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N")));

    [Test]
    public async Task Namespaces_KeepTheirProperties()
    {
        var cache = NewCache();

        await cache.SaveAsync("namespaces.json", [AppHarness.AzureNamespace]);

        (await cache.LoadAsync<BrokerInfo>("namespaces.json")).ShouldBe([AppHarness.AzureNamespace]);
    }

    [Test]
    public async Task Namespaces_CachedBeforeProperties_LoadWithNone()
    {
        var cache = NewCache();
        Directory.CreateDirectory(cache.CacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(cache.CacheDirectory, "namespaces.json"),
            """[{"Name":"sb-a","FullyQualifiedNamespace":"sb-a.servicebus.windows.net","ResourceGroup":"rg","Location":"West Europe","Sku":"Basic"}]""", Ct);

        var loaded = (await cache.LoadAsync<BrokerInfo>("namespaces.json")).ShouldNotBeNull().ShouldHaveSingleItem();

        loaded.Name.ShouldBe("sb-a");
        loaded.Properties.ShouldBeEmpty();
    }

    [Test]
    public async Task RecentBrokers_CachedUnderTheirEarlierNames_Load()
    {
        var cache = NewCache();
        Directory.CreateDirectory(cache.CacheDirectory);
        await File.WriteAllTextAsync(Path.Combine(cache.CacheDirectory, "recent-namespaces.json"),
            """[{"Subscription":{"Name":"Dev","Id":"sub-1"},"Namespace":{"Name":"sb-a","FullyQualifiedNamespace":"sb-a.servicebus.windows.net"},"Opened":"2026-11-01T10:00:00+00:00"}]""", Ct);

        var recent = (await cache.LoadAsync<RecentBroker>("recent-namespaces.json")).ShouldNotBeNull().ShouldHaveSingleItem();

        recent.Connection.ShouldBe(new ConnectionInfo("Dev", "sub-1"));
        recent.Broker.Address.ShouldBe("sb-a.servicebus.windows.net");
    }
}
