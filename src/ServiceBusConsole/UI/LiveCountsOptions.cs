namespace ServiceBusConsole.UI;

/// <summary>How often list pages poll their counts; slow enough to stay clear of Service Bus throttling.</summary>
public sealed class LiveCountsOptions
{
    public TimeSpan Interval { get; set; } = TimeSpan.FromSeconds(15);
}
