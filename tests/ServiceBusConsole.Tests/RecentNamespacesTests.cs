using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

public sealed class RecentNamespacesTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 10, 9, 0, 0, TimeSpan.Zero);
    private static readonly ConnectionInfo Development = AppHarness.Connection;
    private static readonly ConnectionInfo Production = new("Contoso Production", "6f1d2c3b-0000-4000-8000-000000000002");

    private static FileCache NewCache() =>
        new(NullLogger<FileCache>.Instance, Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N")));

    private static BrokerInfo Ns(string name) => AppHarness.AzureNamespace with { Name = name, Address = $"{name}.servicebus.windows.net" };

    private static async Task RecordAsync(RecentBrokers recent, FixedTime time, ConnectionInfo subscription, params string[] names)
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
        var recent = new RecentBrokers(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b");

        recent.Items.Select(r => r.Broker.Name).ShouldBe(["b", "a"]);
        recent.Items[0].Opened.ShouldBe(T0.AddMinutes(1));
        recent.OpenedAt(Development, Ns("a")).ShouldBe(T0);
        recent.OpenedAt(Production, Ns("a")).ShouldBeNull();
    }

    [Test]
    public async Task OpeningAgain_MovesItToTheTop_Once()
    {
        var time = new FixedTime(T0);
        var recent = new RecentBrokers(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b", "a");

        recent.Items.Select(r => r.Broker.Name).ShouldBe(["a", "b"]);
        recent.Items[0].Opened.ShouldBe(T0.AddMinutes(2));
    }

    [Test]
    public async Task TheSameNamespaceName_InAnotherSubscription_IsAnotherEntry()
    {
        var time = new FixedTime(T0);
        var recent = new RecentBrokers(NewCache(), time);

        await RecordAsync(recent, time, Development, "a");
        await RecordAsync(recent, time, Production, "a");

        recent.Items.Select(r => r.Connection).ShouldBe([Production, Development]);
    }

    [Test]
    public async Task KeepsTheLastFive()
    {
        var time = new FixedTime(T0);
        var recent = new RecentBrokers(NewCache(), time);

        await RecordAsync(recent, time, Development, "a", "b", "c", "d", "e", "f", "g");

        recent.Items.Select(r => r.Broker.Name).ShouldBe(["g", "f", "e", "d", "c"]);
    }

    [Test]
    public async Task IsKeptInTheFileCache()
    {
        var cache = NewCache();
        var time = new FixedTime(T0);
        await RecordAsync(new RecentBrokers(cache, time), time, Development, "a", "b");
        await RecordAsync(new RecentBrokers(cache, time), time, ConnectionInfo.Emulator, "localhost");

        var reloaded = new RecentBrokers(cache, time);
        await reloaded.EnsureLoadedAsync();

        reloaded.Items.Select(r => (r.Connection, r.Broker.Name, r.Opened)).ShouldBe(
        [
            (ConnectionInfo.Emulator, "localhost", T0.AddMinutes(2)),
            (Development, "b", T0.AddMinutes(1)),
            (Development, "a", T0),
        ]);
        reloaded.Items[1].Broker.ShouldBe(Ns("b"));
    }
}
