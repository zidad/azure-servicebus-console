namespace ServiceBusConsole.UI;

/// <summary>The metrics panel: what it shows and when it loads. Loads wait for the selection to settle, for Azure Monitor throttling.</summary>
public sealed class MetricsOptions
{
    public TimeSpan Span { get; set; } = TimeSpan.FromHours(24);
    public TimeSpan Interval { get; set; } = TimeSpan.FromMinutes(15);
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>Terminal height from which the panel shows until F12 decides.</summary>
    public int AutoShowHeight { get; set; } = 35;
}
