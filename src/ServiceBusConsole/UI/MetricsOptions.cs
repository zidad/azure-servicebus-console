namespace ServiceBusConsole.UI;

/// <summary>When the metrics panel shows and loads. Loads wait for the selection to settle, for Azure Monitor throttling.</summary>
public sealed class MetricsOptions
{
    public TimeSpan Debounce { get; set; } = TimeSpan.FromMilliseconds(400);

    /// <summary>After + or -: short, only to take a burst of key presses as one change.</summary>
    public TimeSpan RangeDebounce { get; set; } = TimeSpan.FromMilliseconds(150);

    /// <summary>Terminal height from which the panel shows until F12 decides.</summary>
    public int AutoShowHeight { get; set; } = 35;
}
