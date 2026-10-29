namespace ServiceBusConsole.UI;

/// <summary>
/// Holds transient navigation state that cannot be expressed as URL parameters
/// (e.g. the currently selected message object).
/// </summary>
public sealed class NavigationState
{
    public MessageInfo? Message { get; set; }
    public MessageSource? MessageSource { get; set; }
    public string? TopicName { get; set; }
    public string? SubscriptionName { get; set; }
    public string? ReturnPath { get; set; }
    public string? FocusedQueueName { get; set; }
    public AzureSubscription? AzureSubscription { get; set; }

    /// <summary>Changes on <see cref="Reload"/>; the app keys the current page on it.</summary>
    public int PageVersion { get; private set; }

    public event Action? Reloaded;

    /// <summary>Remounts the current page, e.g. after switching namespace on it.</summary>
    public void Reload()
    {
        PageVersion++;
        Reloaded?.Invoke();
    }
}
