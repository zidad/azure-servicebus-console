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

    /// <summary>The next queue or subscription browser opens on its Active tab instead of dead letters; it takes and clears this.</summary>
    public bool StartActive { get; set; }

    /// <summary>The next browser runs this once its first peek is in; it takes and clears this.</summary>
    public BrowserCommand? StartCommand { get; set; }

    /// <summary>Sets what <paramref name="target"/>'s page reads and returns its route.</summary>
    public string RouteTo(JumpTarget target, bool active = false)
    {
        StartActive = active;
        return target.Match(
            q => { FocusedQueueName = q.Info.Name; return $"/queues/{Uri.EscapeDataString(q.Info.Name)}"; },
            t => { TopicName = t.Info.Name; return "/topic-detail"; },
            s =>
            {
                (TopicName, SubscriptionName, ReturnPath) = (s.Info.TopicName, s.Info.Name, "/subscriptions");
                return "/subscription-browser";
            });
    }

    /// <summary>Where the command line asked to start; the first connection page takes it, later visits start as usual.</summary>
    public StartupTarget? Startup { get; set; }

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
