using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.Tests;

public sealed class MetricsTests
{
    private static readonly TimeSpan Quarter = TimeSpan.FromMinutes(15);
    private static readonly DateTimeOffset Now = new(2026, 11, 1, 10, 7, 30, TimeSpan.Zero);
    private static readonly MetricsWindow Day = MetricsWindow.Last(TimeSpan.FromHours(24), Quarter, Now);

    private static FileCache NewCache() =>
        new(NullLogger<FileCache>.Instance, Path.Combine(Path.GetTempPath(), "sb-console-tests", Guid.NewGuid().ToString("N")));

    [Test]
    public void Window_EndsWithTheIntervalHoldingNow()
    {
        Day.To.ShouldBe(new DateTimeOffset(2026, 11, 1, 10, 15, 0, TimeSpan.Zero));
        Day.From.ShouldBe(Day.To.AddHours(-24));
        Day.IntervalStarts().Count().ShouldBe(96);
        Day.IntervalOf(Now).ShouldBe(new DateTimeOffset(2026, 11, 1, 10, 0, 0, TimeSpan.Zero));
        Day.IntervalOf(Day.To).ShouldBeNull();
        Day.IntervalOf(Day.From.AddTicks(-1)).ShouldBeNull();
    }

    [Test]
    public void Within_DropsPointsOutsideTheWindow()
    {
        var yesterday = FakeMetricsService.Sample(MetricsWindow.Last(TimeSpan.FromHours(24), Quarter, Now.AddHours(-3)), 1);

        var moved = yesterday.Within(Day);

        moved.Window.ShouldBe(Day);
        moved.Incoming.Count.ShouldBe(96 - 12);
        moved.Incoming.ShouldAllBe(p => p.Time >= Day.From);
    }

    [Test]
    public void Steps_HoldTheLastIntervalUntilTheWindowEnds()
    {
        var points = Day.IntervalStarts().Select((t, i) => new MetricPoint(t, i)).ToList();

        var steps = MetricsChart.Steps(points, Day);

        steps.Count.ShouldBe(97);
        steps[0].ShouldBe((-24.0, 0.0));
        steps[^2].ShouldBe((-0.25, 95.0));
        steps[^1].ShouldBe((0.0, 95.0));
        MetricsChart.Steps([], Day).ShouldBeEmpty();
    }

    [Test]
    [Arguments(9_999, "#,0.#")]
    [Arguments(10_000, "0.#,'k'")]
    [Arguments(2_500_000, "0.#,,'M'")]
    public void YAxisFormat_StaysNarrowForLargeCounts(double max, string format) =>
        MetricsChart.YAxisFormat([[new MetricPoint(Day.From, 0)], [new MetricPoint(Day.From, max)]]).ShouldBe(format);

    [Test]
    public void Fit_CutsByTerminalCells()
    {
        CellText.Fit("📬 queue orders", 15).ShouldBe("📬 queue orders");
        CellText.Fit("📬 queue orders", 10).ShouldBe("📬 queue…");
    }

    [Test]
    public async Task Caching_ShowsCachedMetricsInTheWindowFirst_ThenLive()
    {
        var connection = new ServiceBusConnection();
        connection.SetNamespace(AppHarness.AzureNamespace);
        var cache = NewCache();
        var live = new FakeMetricsService();
        var service = new CachingMetricsService(live, connection, cache);
        var scope = new MetricsScope.Entity("orders");
        var earlier = MetricsWindow.Last(TimeSpan.FromHours(24), Quarter, Now.AddHours(-1));

        (await service.GetMetricsAsync(scope, earlier, Ct).ToListAsync(Ct)).ShouldHaveSingleItem().IsRefreshing.ShouldBeFalse();
        await WaitForCacheAsync(cache);
        var results = await service.GetMetricsAsync(scope, Day, Ct).ToListAsync(Ct);

        results.Count.ShouldBe(2);
        results[0].IsRefreshing.ShouldBeTrue();
        results[0].Window.ShouldBe(Day);
        results[0].Incoming.Count.ShouldBe(96 - 4);
        results[1].IsRefreshing.ShouldBeFalse();
        results[1].Incoming.Count.ShouldBe(96);
    }

    [Test]
    public async Task Caching_SkipsCachedMetricsOlderThanTheWindow()
    {
        var connection = new ServiceBusConnection();
        connection.SetNamespace(AppHarness.AzureNamespace);
        var cache = NewCache();
        var service = new CachingMetricsService(new FakeMetricsService(), connection, cache);
        var lastWeek = MetricsWindow.Last(TimeSpan.FromHours(24), Quarter, Now.AddDays(-7));

        await service.GetMetricsAsync(new MetricsScope.Namespace(), lastWeek, Ct).ToListAsync(Ct);
        await WaitForCacheAsync(cache);

        (await service.GetMetricsAsync(new MetricsScope.Namespace(), Day, Ct).ToListAsync(Ct)).ShouldHaveSingleItem().IsRefreshing.ShouldBeFalse();
    }

    [Test]
    public void Visibility_DependsOnTheTerminalHeightUntilChosen()
    {
        var visibility = new MetricsVisibility(NewCache(), new MetricsOptions());

        visibility.IsVisible(34).ShouldBeFalse();
        visibility.IsVisible(35).ShouldBeTrue();
    }

    [Test]
    public async Task Visibility_RemembersTheChoice_ButNeverCrowdsOutTheList()
    {
        var cache = NewCache();
        await new MetricsVisibility(cache, new MetricsOptions()).ToggleAsync(30);

        var next = new MetricsVisibility(cache, new MetricsOptions());
        await next.LoadAsync();

        next.IsVisible(30).ShouldBeTrue();
        next.IsVisible(20).ShouldBeFalse();
        await next.ToggleAsync(50);
        next.IsVisible(50).ShouldBeFalse();
    }

    // The decorator saves in the background.
    private static async Task WaitForCacheAsync(FileCache cache)
    {
        while (!await IsSavedAsync(cache)) await Task.Delay(10, Ct);
    }

    private static async Task<bool> IsSavedAsync(FileCache cache) =>
        Directory.Exists(cache.CacheDirectory)
        && await Directory.EnumerateFiles(cache.CacheDirectory, "*.json", SearchOption.AllDirectories).ToAsyncEnumerable()
            .AnyAsync(async (file, _) => await cache.LoadAsync<MessageMetrics>(Path.GetRelativePath(cache.CacheDirectory, file)) is [_], Ct);
}
