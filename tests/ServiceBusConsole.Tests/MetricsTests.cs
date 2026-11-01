using System.Globalization;
using Microsoft.AspNetCore.Components.Web;
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
    public void Steps_CountInTheAxisUnit()
    {
        var hour = MetricsWindow.Last(TimeSpan.FromHours(1), TimeSpan.FromMinutes(1), Now);
        var week = MetricsWindow.Last(TimeSpan.FromDays(7), TimeSpan.FromHours(6), Now);

        MetricsChart.Steps([new MetricPoint(hour.From, 1)], hour).ShouldBe([(-60.0, 1.0), (-59.0, 1.0)]);
        MetricsChart.Steps([new MetricPoint(week.From, 1)], week).ShouldBe([(-7.0, 1.0), (-6.75, 1.0)]);
    }

    [Test]
    [Arguments(60, "m", -60, "-60m")]
    [Arguments(3 * 60, "h", -3, "-3h")]
    [Arguments(24 * 60, "h", -24, "-24h")]
    [Arguments(3 * 24 * 60, "d", -3, "-3d")]
    [Arguments(30 * 24 * 60, "d", -30, "-30d")]
    public void TimeAxis_CountsMinutesUpToAnHour_HoursUpToADay_ThenDays(int spanMinutes, string suffix, double value, string label)
    {
        var axis = TimeAxis.For(TimeSpan.FromMinutes(spanMinutes));

        axis.Suffix.ShouldBe(suffix);
        value.ToString(axis.Format, CultureInfo.InvariantCulture).ShouldBe(label);
        0.0.ToString(axis.Format, CultureInfo.InvariantCulture).ShouldBe("now");
    }

    [Test]
    [Arguments(60, 60, 1)]
    [Arguments(60, 40, 1)]
    [Arguments(60, 39, 5)]
    [Arguments(24 * 60, 64, 15)]
    [Arguments(24 * 60, 63, 30)]
    [Arguments(7 * 24 * 60, 112, 60)]
    [Arguments(7 * 24 * 60, 111, 6 * 60)]
    [Arguments(30 * 24 * 60, 40, 12 * 60)]
    [Arguments(30 * 24 * 60, 39, 24 * 60)]
    [Arguments(30 * 24 * 60, 19, 24 * 60)]
    public void Interval_IsTheFinestGrainWithAtMostOneAndAHalfPointsPerColumn(int spanMinutes, int columns, int grainMinutes) =>
        MetricsChart.Interval(TimeSpan.FromMinutes(spanMinutes), columns).ShouldBe(TimeSpan.FromMinutes(grainMinutes));

    [Test]
    public void Interval_OnA120ColumnTerminal()
    {
        // Each chart is (120 - 2) / 2 - 2 = 57 wide.
        var columns = MetricsChart.PlotColumns(57);

        columns.ShouldBe(50);
        MetricsRange.Steps.Select(span => MetricsChart.Interval(span, columns).TotalMinutes).ShouldBe(
            [1, 5, 5, 15, 30, 60, 6 * 60, 6 * 60, 12 * 60]);
    }

    [Test]
    public void Range_StepsFromAnHourToThirtyDays_StoppingAtTheEnds()
    {
        TimeSpan[] steps = [.. MetricsRange.Steps];

        steps.Select(s => s.TotalHours).ShouldBe([1, 3, 6, 12, 24, 3 * 24, 7 * 24, 14 * 24, 30 * 24]);
        MetricsRange.Step(TimeSpan.FromDays(1), 1).ShouldBe(TimeSpan.FromDays(3));
        MetricsRange.Step(TimeSpan.FromDays(1), -1).ShouldBe(TimeSpan.FromHours(12));
        MetricsRange.Step(TimeSpan.FromHours(1), -1).ShouldBe(TimeSpan.FromHours(1));
        MetricsRange.Step(TimeSpan.FromDays(30), 1).ShouldBe(TimeSpan.FromDays(30));
        MetricsRange.Step(TimeSpan.FromDays(1), 10).ShouldBe(TimeSpan.FromDays(30));
    }

    [Test]
    [Arguments("+", null, -1)]
    [Arguments("=", null, -1)]
    [Arguments("+", "orders", -1)]
    [Arguments("=", "orders", -1)]
    [Arguments("-", null, 1)]
    [Arguments("_", "", 1)]
    [Arguments("-", "orders", null)]
    [Arguments("_", "orders", null)]
    [Arguments("o", null, null)]
    [Arguments("F5", null, null)]
    public void Range_PlusOrEqualsZoomsIn_MinusOrUnderscoreZoomsOutOnlyWithoutAFilter(string key, string? filter, int? step) =>
        MetricsRange.StepOf(new KeyboardEventArgs { Key = key }, filter).ShouldBe(step);

    [Test]
    public void Range_Label_KeepsOneWidth()
    {
        MetricsRange.Label(TimeSpan.FromDays(1)).ShouldBe("last 24 h");
        MetricsRange.Label(TimeSpan.FromHours(1)).ShouldBe(" last 1 h");
        MetricsRange.Label(TimeSpan.FromDays(7)).ShouldBe(" last 7 d");
        MetricsRange.Steps.Select(MetricsRange.Label).ShouldAllBe(label => label.Length == 9);
    }

    [Test]
    public async Task Range_StartsAtADay_AndRemembersTheChoice()
    {
        var cache = NewCache();
        var range = new MetricsRange(cache);
        range.Span.ShouldBe(TimeSpan.FromDays(1));

        await range.StepAsync(2);
        await range.StepAsync(-10);
        await range.StepAsync(1);

        var next = new MetricsRange(cache);
        await next.LoadAsync();
        next.Span.ShouldBe(TimeSpan.FromHours(3));
        next.CanNarrow.ShouldBeTrue();
        next.CanWiden.ShouldBeTrue();
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
        var cache = NewCache();
        var live = new FakeMetricsService();
        var service = new CachingMetricsService(live, AppHarness.AzureNamespace, cache);
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
        var cache = NewCache();
        var service = new CachingMetricsService(new FakeMetricsService(), AppHarness.AzureNamespace, cache);
        var lastWeek = MetricsWindow.Last(TimeSpan.FromHours(24), Quarter, Now.AddDays(-7));

        await service.GetMetricsAsync(new MetricsScope.Namespace(), lastWeek, Ct).ToListAsync(Ct);
        await WaitForCacheAsync(cache);

        (await service.GetMetricsAsync(new MetricsScope.Namespace(), Day, Ct).ToListAsync(Ct)).ShouldHaveSingleItem().IsRefreshing.ShouldBeFalse();
    }

    [Test]
    public async Task Caching_KeepsEachSpanAndIntervalApart()
    {
        var cache = NewCache();
        var service = new CachingMetricsService(new FakeMetricsService(), AppHarness.AzureNamespace, cache);
        var scope = new MetricsScope.Entity("orders");
        var halfHours = MetricsWindow.Last(TimeSpan.FromHours(24), TimeSpan.FromMinutes(30), Now);
        var week = MetricsWindow.Last(TimeSpan.FromDays(7), TimeSpan.FromHours(6), Now);

        await service.GetMetricsAsync(scope, Day, Ct).ToListAsync(Ct);
        await WaitForCacheAsync(cache);

        (await service.GetMetricsAsync(scope, halfHours, Ct).ToListAsync(Ct)).ShouldHaveSingleItem().IsRefreshing.ShouldBeFalse();
        (await service.GetMetricsAsync(scope, week, Ct).ToListAsync(Ct)).ShouldHaveSingleItem().IsRefreshing.ShouldBeFalse();
        (await service.GetMetricsAsync(scope, Day, Ct).ToListAsync(Ct)).Count.ShouldBe(2);
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

    [Test]
    public async Task Visibility_TogglesTheChoice_AlsoWhereThePanelDoesNotFit()
    {
        var visibility = new MetricsVisibility(NewCache(), new MetricsOptions());

        await visibility.ToggleAsync(40);
        visibility.IsVisible(50).ShouldBeFalse();

        await visibility.ToggleAsync(40);
        visibility.IsVisible(40, minHeight: 45).ShouldBeFalse();
        visibility.IsVisible(45, minHeight: 45).ShouldBeTrue();
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
