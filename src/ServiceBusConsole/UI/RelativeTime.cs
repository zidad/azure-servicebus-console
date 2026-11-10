using System.Globalization;

namespace ServiceBusConsole.UI;

/// <summary>"12 min ago": how long ago a time was, in its largest whole unit, for lists; details show the absolute time.</summary>
public static class RelativeTime
{
    public static string Ago(DateTimeOffset time, DateTimeOffset now) => (now - time) switch
    {
        var d when d < TimeSpan.FromSeconds(1) => UIStrings.RelativeTime_Now,
        var d when d < TimeSpan.FromMinutes(1) => UIStrings.FormatRelativeTime_SecondsAgo((int)d.TotalSeconds),
        var d when d < TimeSpan.FromHours(1) => UIStrings.FormatRelativeTime_MinutesAgo((int)d.TotalMinutes),
        var d when d < TimeSpan.FromDays(1) => UIStrings.FormatRelativeTime_HoursAgo((int)d.TotalHours),
        var d => UIStrings.FormatRelativeTime_DaysAgo((int)d.TotalDays),
    };

    /// <summary>"10-26 10:17:00 · 2 h ago", local time; the year only when it isn't this one.</summary>
    public static string Full(DateTimeOffset time, DateTimeOffset now) =>
        $"{time.LocalDateTime.ToString(time.Year == now.Year ? "MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} · {Ago(time, now)}";
}
