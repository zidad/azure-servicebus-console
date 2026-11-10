namespace ServiceBusConsole.UI;

/// <summary>A chart's X axis: time before the window's end, in minutes up to an hour, hours up to a day, days beyond.</summary>
public readonly record struct TimeAxis(TimeSpan Unit, string Suffix)
{
    public static TimeAxis For(TimeSpan span) =>
        span <= TimeSpan.FromHours(1) ? new(TimeSpan.FromMinutes(1), "m")
        : span <= TimeSpan.FromDays(1) ? new(TimeSpan.FromHours(1), "h")
        : new(TimeSpan.FromDays(1), "d");

    /// <summary>"-60m" … "now".</summary>
    public string Format => $"0'{Suffix}';-0'{Suffix}';'{UIStrings.RelativeTime_Now}'";

    public double Value(DateTimeOffset time, DateTimeOffset end) => (time - end) / Unit;
}
