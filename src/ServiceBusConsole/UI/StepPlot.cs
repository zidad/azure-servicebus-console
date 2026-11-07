using System.Globalization;

namespace ServiceBusConsole.UI;

/// <summary>
/// Step series drawn with box-drawing characters, as lines of colored runs: Y labels and axis, plot, X axis and X labels.
/// <see cref="Width"/> + 2 columns wide and <see cref="Height"/> + 1 lines high, of which <see cref="Height"/> - 2 plot rows.
/// </summary>
public sealed record StepPlot
{
    private const int MaxAxisLabels = 5;
    private const int MinYLabelWidth = 2;
    private const char Horizontal = '─';
    private const char Vertical = '│';
    private const char CornerRightUp = '┘';
    private const char CornerRightDown = '┐';
    private const char CornerLeftUp = '┌';
    private const char CornerLeftDown = '└';

    /// <summary>Drawn in order: a later series covers an earlier one where they meet.</summary>
    public required IReadOnlyList<ChartSeries> Series { get; init; }

    public required int Width { get; init; }

    public required int Height { get; init; }

    public Color AxesColor { get; init; } = Color.Default;

    public Color LabelsColor { get; init; } = Color.Default;

    /// <summary>.NET numeric format of the X labels, in the current culture.</summary>
    public string XAxisFormat { get; init; } = "0.#";

    /// <summary>.NET numeric format of the Y labels, in the current culture.</summary>
    public string YAxisFormat { get; init; } = "0.#";

    public IReadOnlyList<IReadOnlyList<ChartRun>> Lines()
    {
        var series = Series
            .Select(s => s with { Points = [.. s.Points.Where(p => double.IsFinite(p.X) && double.IsFinite(p.Y)).OrderBy(p => p.X)] })
            .ToArray();
        if (series.SelectMany(s => s.Points).ToArray() is not { Length: > 0 } points)
            return [[new ChartRun("No data", LabelsColor)]];

        var x = new AxisRange(points.Min(p => p.X), points.Max(p => p.X));
        var y = new AxisRange(Math.Min(0, points.Min(p => p.Y)), points.Max(p => p.Y));
        var rows = Math.Max(1, Height - 2);
        var yLabels = RowLabels(y, rows);
        var labelWidth = yLabels.Select(label => label?.Length ?? 0).Prepend(MinYLabelWidth).Max();
        // The gutter holds the Y labels plus "─┤"; the X axis ends in "▶" one column past the plot.
        var gutter = labelWidth + 2;
        var columns = Math.Max(1, Width + 1 - gutter);
        var canvas = Draw(series, x, y, columns, rows);
        var blankLabel = new string(' ', labelWidth + 1);

        var lines = new List<IReadOnlyList<ChartRun>> { new Line().Add(blankLabel + "▲", AxesColor).Runs };
        for (var row = rows - 1; row >= 0; row--)
        {
            var line = yLabels[row] is { } label
                ? new Line().Add(new string(' ', labelWidth - label.Length) + label, LabelsColor).Add("─┤", AxesColor)
                : new Line().Add(blankLabel + "│", AxesColor);
            for (var col = 0; col < columns; col++)
                line.Add(canvas[col, row].Ch.ToString(), canvas[col, row].Color);
            lines.Add(line.Runs);
        }

        var xValues = AxisValues(x);
        var ticks = xValues.Select(value => x.Scale(value, columns)).ToHashSet();
        var axis = string.Concat(Enumerable.Range(0, columns).Select(col => ticks.Contains(col) ? '┬' : '─'));
        lines.Add(new Line().Add(blankLabel + "└" + axis + "▶", AxesColor).Runs);
        lines.Add(XLabels(xValues, x, gutter, columns).Runs);
        return lines;
    }

    private string?[] RowLabels(AxisRange range, int rows)
    {
        var labels = new string?[rows];
        if (!range.HasSpan)
        {
            labels[0] = Format(range.Min, YAxisFormat);
            return labels;
        }

        var values = AxisValues(range);
        for (var row = 0; row < rows; row++)
        {
            var rowValue = rows == 1 ? range.Min : range.Min + range.Span * row / (rows - 1);
            var closest = values.MinBy(value => Math.Abs(value - rowValue));
            if (Math.Abs(closest - rowValue) < range.Span / (rows * 2))
                labels[row] = Format(closest, YAxisFormat);
        }

        return labels;
    }

    private static List<double> AxisValues(AxisRange range)
    {
        if (!range.HasSpan) return [range.Min];

        var roughStep = range.Span / (MaxAxisLabels - 1);
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(roughStep)));
        var step = magnitude * (roughStep / magnitude) switch
        {
            < 1.5 => 1,
            < 3 => 2,
            < 7 => 5,
            _ => 10,
        };

        // step >= roughStep / 1.5, so at most 7 steps fit in the range.
        var start = Math.Ceiling(range.Min / step) * step;
        var values = Enumerable.Range(0, MaxAxisLabels * 2)
            .Select(i => start + i * step)
            .TakeWhile(value => value <= range.Max)
            .ToList();
        if (values.Count == 0 || values[0] > range.Min) values.Insert(0, range.Min);
        if (values[^1] < range.Max) values.Add(range.Max);
        return values;
    }

    private Line XLabels(List<double> values, AxisRange x, int gutter, int columns)
    {
        var line = new Line();
        var total = gutter + columns + 1;
        var end = 0;
        var next = gutter;
        foreach (var value in values)
        {
            var label = Format(value, XAxisFormat);
            var start = Math.Min(Math.Max(next, gutter + x.Scale(value, columns) - label.Length / 2), total - label.Length);
            if (start < next) continue;
            line.Add(new string(' ', start - end), Color.Default).Add(label, LabelsColor);
            end = start + label.Length;
            next = end + 1;
        }

        return line;
    }

    private static (char Ch, Color Color)[,] Draw(IEnumerable<ChartSeries> series, AxisRange x, AxisRange y, int columns, int rows)
    {
        var canvas = new (char Ch, Color Color)[columns, rows];
        for (var col = 0; col < columns; col++)
            for (var row = 0; row < rows; row++)
                canvas[col, row] = (' ', Color.Default);

        foreach (var (color, points) in series)
        {
            for (var i = 0; i < points.Count - 1; i++)
            {
                var col = x.Scale(points[i].X, columns);
                var row = y.Scale(points[i].Y, rows);
                var nextCol = x.Scale(points[i + 1].X, columns);
                var nextRow = y.Scale(points[i + 1].Y, rows);

                for (var c = col; c < nextCol; c++)
                {
                    if (canvas[c, row].Ch is not (Vertical or CornerRightUp or CornerRightDown or CornerLeftUp or CornerLeftDown))
                        canvas[c, row] = (Horizontal, color);
                }

                if (nextRow == row) continue;
                for (var r = Math.Min(row, nextRow) + 1; r < Math.Max(row, nextRow); r++)
                    canvas[nextCol, r] = (Vertical, color);
                var up = nextRow > row;
                canvas[nextCol, row] = (up ? CornerRightUp : CornerRightDown, color);
                canvas[nextCol, nextRow] = (up ? CornerLeftUp : CornerLeftDown, color);
            }
        }

        return canvas;
    }

    // Labels are display text, so they use the current culture; -0 prints as 0.
    private static string Format(double value, string format) => (value == 0 ? 0 : value).ToString(format, CultureInfo.CurrentCulture);

    private readonly record struct AxisRange(double Min, double Max)
    {
        public bool HasSpan => Max > Min;

        public double Span => Max - Min;

        public int Scale(double value, int size) =>
            HasSpan ? Math.Clamp((int)Math.Round((value - Min) / Span * (size - 1)), 0, size - 1) : 0;
    }

    // Adjacent text of one color becomes one run.
    private sealed class Line
    {
        private readonly List<ChartRun> _runs = [];

        public IReadOnlyList<ChartRun> Runs => _runs;

        public Line Add(string text, Color color)
        {
            if (text.Length == 0) return this;
            if (_runs is [.., var last] && last.Color == color) _runs[^1] = last with { Text = last.Text + text };
            else _runs.Add(new ChartRun(text, color));
            return this;
        }
    }
}
