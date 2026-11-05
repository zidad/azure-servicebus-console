using System.Globalization;

namespace ServiceBusConsole.UI;

/// <summary>"12 min ago": how long ago a time was, in its largest whole unit, for lists; details show the absolute time.</summary>
public static class RelativeTime
{
    public static string Ago(DateTimeOffset time, DateTimeOffset now) => (now - time) switch
    {
        var d when d < TimeSpan.FromSeconds(1) => "now",
        var d when d < TimeSpan.FromMinutes(1) => $"{(int)d.TotalSeconds} s ago",
        var d when d < TimeSpan.FromHours(1) => $"{(int)d.TotalMinutes} min ago",
        var d when d < TimeSpan.FromDays(1) => $"{(int)d.TotalHours} h ago",
        var d => $"{(int)d.TotalDays} d ago",
    };

    /// <summary>"10-26 10:17:00 · 2 h ago", local time; the year only when it isn't this one.</summary>
    public static string Full(DateTimeOffset time, DateTimeOffset now) =>
        $"{time.LocalDateTime.ToString(time.Year == now.Year ? "MM-dd HH:mm:ss" : "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture)} · {Ago(time, now)}";
}
