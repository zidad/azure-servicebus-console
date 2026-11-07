namespace ServiceBusConsole.UI;

/// <summary>One series of a <see cref="StepPlot"/>: each point's Y held until the next point's X.</summary>
public sealed record ChartSeries(Color Color, IReadOnlyList<(double X, double Y)> Points);
