using Microsoft.AspNetCore.Components.Web;

namespace ServiceBusConsole.UI;

/// <summary>How far back the metrics charts go: a day until zooming in (+) or out (-) steps it, then that choice, kept across runs.</summary>
public sealed class MetricsRange(FileCache cache)
{
    private const string Key = "settings/metrics-range.json";

    private static readonly TimeSpan[] All =
    [
        TimeSpan.FromHours(1), TimeSpan.FromHours(3), TimeSpan.FromHours(6), TimeSpan.FromHours(12),
        TimeSpan.FromDays(1), TimeSpan.FromDays(3), TimeSpan.FromDays(7), TimeSpan.FromDays(14), TimeSpan.FromDays(30),
    ];

    /// <summary>Shortest first; Azure Monitor keeps platform metrics for 93 days.</summary>
    public static IReadOnlyList<TimeSpan> Steps => All;

    public TimeSpan Span { get; private set; } = TimeSpan.FromDays(1);

    public bool CanWiden => Span < All[^1];
    public bool CanNarrow => Span > All[0];

    /// <summary><paramref name="span"/> moved <paramref name="steps"/> along <see cref="Steps"/>, stopping at either end.</summary>
    public static TimeSpan Step(TimeSpan span, int steps) =>
        All[Math.Clamp(Array.IndexOf(All, span) + steps, 0, All.Length - 1)];

    /// <summary>
    /// Zoom in (+ or =, shorter range) is -1, zoom out (- or _, longer range) +1, other keys null; = and _ work without
    /// Shift. A list filter being edited takes its keys first (<see cref="ListFilter"/>), so - and _ go there then.
    /// </summary>
    public static int? StepOf(KeyboardEventArgs e) => e.Key switch
    {
        "+" or "=" => -1,
        "-" or "_" => 1,
        _ => null,
    };

    /// <summary>"last 24 h", "last 7 d", padded to one width so the buttons around it stay put.</summary>
    public static string Label(TimeSpan span) =>
        (span <= TimeSpan.FromDays(1) ? $"last {span.TotalHours:0} h" : $"last {span.TotalDays:0} d").PadLeft(9);

    /// <summary>Reads the remembered span; call once at startup.</summary>
    public async Task LoadAsync()
    {
        if (await cache.LoadAsync<TimeSpan>(Key) is [var span] && All.Contains(span)) Span = span;
    }

    /// <summary>Steps the span at once; the returned task saves it.</summary>
    public Task StepAsync(int steps)
    {
        var span = Step(Span, steps);
        if (span == Span) return Task.CompletedTask;
        Span = span;
        return cache.SaveAsync(Key, [span]);
    }
}
