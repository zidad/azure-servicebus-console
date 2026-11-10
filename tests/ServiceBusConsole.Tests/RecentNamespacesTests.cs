using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

public sealed class RecentNamespacesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly AzureSubscription Development = AppHarness.AzureSubscription;
    private static readonly AzureSubscription Production = new("Contoso Production", "6f1d2c3b-0000-4000-8000-000000000002");

    private static FileCache NewCache() =>
        new(NullLogger<FileCache>.Instance, Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N")));

    private static NamespaceInfo Ns(string name) => AppHarness.AzureNamespace with { Name = name, FullyQualifiedNamespace = $"{name}.servicebus.windows.net" };

    private static async Task RecordAsync(RecentNamespaces recent, FixedTime time, AzureSubscription subscription, params string[] names)
    {
        foreach (var name in names)
        {
            await recent.RecordAsync(subscription, Ns(name));
            time.Now += TimeSpan.FromMinutes(1);
        }
    }

    [Test]
    public async Task NewestFirst_WithTheTimeOpened()
    {
        var time = new FixedTime(T0);
        var recent = new RecentNamespaces(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b");

        recent.Items.Select(r => r.Namespace.Name).ShouldBe(["b", "a"]);
        recent.Items[0].Opened.ShouldBe(T0.AddMinutes(1));
        recent.OpenedAt(Development, Ns("a")).ShouldBe(T0);
        recent.OpenedAt(Production, Ns("a")).ShouldBeNull();
    }

    [Test]
    public async Task OpeningAgain_MovesItToTheTop_Once()
    {
        var time = new FixedTime(T0);
        var recent = new RecentNamespaces(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b", "a");

        recent.Items.Select(r => r.Namespace.Name).ShouldBe(["a", "b"]);
        recent.Items[0].Opened.ShouldBe(T0.AddMinutes(2));
    }

    [Test]
    public async Task TheSameNamespaceName_InAnotherSubscription_IsAnotherEntry()
    {
        var time = new FixedTime(T0);
        var recent = new RecentNamespaces(NewCache(), time);

        await RecordAsync(recent, time, Development, "a");
        await RecordAsync(recent, time, Production, "a");

        recent.Items.Select(r => r.Subscription).ShouldBe([Production, Development]);
    }

    [Test]
    public async Task KeepsTheLastFive()
    {
        var time = new FixedTime(T0);
        var recent = new RecentNamespaces(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b", "c", "d", "e", "f", "g");

        recent.Items.Select(r => r.Namespace.Name).ShouldBe(["g", "f", "e", "d", "c"]);
    }

    [Test]
    public async Task IsKeptInTheFileCache()
    {
        var cache = NewCache();
        var time = new FixedTime(T0);
        await RecordAsync(new RecentNamespaces(cache, time), time, Development, "a", "b");
        await RecordAsync(new RecentNamespaces(cache, time), time, AzureSubscription.Emulator, "localhost");

        var reloaded = new RecentNamespaces(cache, time);
        await reloaded.EnsureLoadedAsync();

        reloaded.Items.Select(r => (r.Subscription, r.Namespace.Name, r.Opened)).ShouldBe(
        [
            (AzureSubscription.Emulator, "localhost", T0.AddMinutes(2)),
            (Development, "b", T0.AddMinutes(1)),
            (Development, "a", T0),
        ]);
        reloaded.Items[1].Namespace.ShouldBe(Ns("b"));
    }
}
