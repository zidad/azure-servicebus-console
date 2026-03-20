namespace ServiceBusConsole;

/// <summary>
/// Holds transient navigation state that cannot be expressed as URL parameters
/// (e.g. the currently selected message object).
/// </summary>
public class NavigationState
{
    public MessageInfo? Message { get; set; }
    public MessageSource? MessageSource { get; set; }
}
