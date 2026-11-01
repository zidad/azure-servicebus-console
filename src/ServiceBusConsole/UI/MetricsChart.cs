using RazorConsole.Components;

namespace ServiceBusConsole.UI;

/// <summary>Turns <see cref="MessageMetrics"/> series into <see cref="StepChart"/> series, X in hours before the window's end.</summary>
public static class MetricsChart
{
    public const string XAxisFormat = "0'h';-0'h';'now'";

    /// <summary>Drawn in order: a later series covers an earlier one where they meet.</summary>
    public static List<StepChart.ChartSeries> Series(MetricsWindow window, params IEnumerable<(Color Color, IReadOnlyList<MetricPoint> Points)> series) =>
        [.. series.Select(s => new StepChart.ChartSeries(s.Color, Steps(s.Points, window)))];

    /// <summary>Each interval's value held until the next one starts; the last one until its own end.</summary>
    public static List<(double X, double Y)> Steps(IReadOnlyList<MetricPoint> points, MetricsWindow window) =>
        points is [.., var last]
            ? [.. points.Select(p => (Hours(p.Time, window), p.Value)), (Hours(last.Time + window.Interval, window), last.Value)]
            : [];

    /// <summary>Compact labels for large counts, so the axis stays narrow.</summary>
    public static string YAxisFormat(IEnumerable<IReadOnlyList<MetricPoint>> series) =>
        series.SelectMany(s => s).Select(p => p.Value).DefaultIfEmpty().Max() switch
        {
            >= 1_000_000 => "0.#,,'M'",
            >= 10_000 => "0.#,'k'",
            _ => "#,0.#",
        };

    private static double Hours(DateTimeOffset time, MetricsWindow window) => (time - window.To).TotalHours;
}
