namespace ServiceBusConsole.UI;

/// <summary>Turns <see cref="MessageMetrics"/> series into <see cref="StepPlot"/> series, X in <see cref="TimeAxis"/> units before the window's end.</summary>
public static class MetricsChart
{
    // The widest Y label of the compact formats, e.g. "123.4k".
    private const int MaxYLabelWidth = 6;

    /// <summary>Azure Monitor's metric time grains, finest first.</summary>
    public static IReadOnlyList<TimeSpan> Grains { get; } =
    [
        TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(30),
        TimeSpan.FromHours(1), TimeSpan.FromHours(6), TimeSpan.FromHours(12), TimeSpan.FromDays(1),
    ];

    /// <summary>Plot columns of a <see cref="StepPlot"/> of <paramref name="width"/>: its Y labels and axes take the rest.</summary>
    public static int PlotColumns(int width) => width - 1 - MaxYLabelWidth;

    /// <summary>
    /// The finest grain with at most one and a half points per column over <paramref name="span"/>; the coarsest when none fits.
    /// Allowing a bit over one point per column keeps 3 d hourly instead of 6-hourly: Azure has no grain in between.
    /// </summary>
    public static TimeSpan Interval(TimeSpan span, int columns) =>
        Grains.FirstOrDefault(grain => span / grain <= columns * 1.5, Grains[^1]);

    public static string XAxisFormat(MetricsWindow window) => TimeAxis.For(window.To - window.From).Format;

    /// <summary>Drawn in order: a later series covers an earlier one where they meet.</summary>
    public static IReadOnlyList<ChartSeries> Series(MetricsWindow window, params IEnumerable<(Color Color, IReadOnlyList<MetricPoint> Points)> series) =>
        [.. series.Select(s => new ChartSeries(s.Color, Steps(s.Points, window)))];

    /// <summary>Each interval's value held until the next one starts; the last one until its own end.</summary>
    public static List<(double X, double Y)> Steps(IReadOnlyList<MetricPoint> points, MetricsWindow window)
    {
        var axis = TimeAxis.For(window.To - window.From);
        return points is [.., var last]
            ? [.. points.Select(p => (axis.Value(p.Time, window.To), p.Value)), (axis.Value(last.Time + window.Interval, window.To), last.Value)]
            : [];
    }

    /// <summary>Compact labels for large counts, so the axis stays narrow.</summary>
    public static string YAxisFormat(IEnumerable<IReadOnlyList<MetricPoint>> series) =>
        series.SelectMany(s => s).Select(p => p.Value).DefaultIfEmpty().Max() switch
        {
            >= 1_000_000 => "0.#,,'M'",
            >= 10_000 => "0.#,'k'",
            _ => "#,0.#",
        };
}
