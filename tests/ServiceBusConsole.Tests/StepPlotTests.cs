using System.Globalization;
using Spectre.Console;


namespace ServiceBusConsole.Tests;

public sealed class StepPlotTests
{
    private static string Text(StepPlot plot) =>
        string.Join("\n", plot.Lines().Select(line => string.Concat(line.Select(run => run.Text))));

    [Test]
    public void Lines_DrawTheStepsBetweenTheAxes()
    {
        CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;
        var plot = new StepPlot
        {
            Width = 14,
            Height = 6,
            Series = [new ChartSeries(Color.Blue, [(0, 0), (2, 2), (4, 1), (6, 1)])],
        };

        Text(plot).Split('\n').ShouldBe(
        [
            "    ▲",
            "  2─┤   ┌──┐   ",
            "1.5─┤   │  └── ",
            "0.5─┤   │      ",
            "  0─┤───┘      ",
            "    └┬──┬──┬──┬▶",
            "     0  2  4  6",
        ]);
    }

    [Test]
    public void Lines_ColorTheSeriesAxesAndLabels()
    {
        var lines = new StepPlot
        {
            Width = 14,
            Height = 6,
            AxesColor = Color.Grey,
            LabelsColor = Color.Silver,
            Series = [new ChartSeries(Color.Blue, [(0, 0), (2, 2)])],
        }.Lines();

        lines[1].ShouldBe([new ChartRun("  2", Color.Silver), new ChartRun("─┤", Color.Grey), new ChartRun("         ", Color.Default), new ChartRun("┌", Color.Blue)]);
    }

    [Test]
    public void Lines_WithoutPoints_SayNoData() =>
        new StepPlot { Width = 20, Height = 6, LabelsColor = Color.Grey, Series = [new ChartSeries(Color.Blue, [])] }
            .Lines().ShouldBe([[new ChartRun("No data", Color.Grey)]]);
}
