namespace ServiceBusConsole.Messaging;

/// <summary>Label and confirm text for F10, shared by the browser and the detail page.</summary>
public static class RetryText
{
    /// <summary>Retry/Requeue is available in a dead-letter queue, and for received-and-deleted messages.</summary>
    public static bool CanRetry(BrowseMode mode, bool isDlq) => isDlq || mode == BrowseMode.Received;

    public static string Label(bool isDlq) => isDlq ? "Retry" : "Requeue";

    /// <summary>Short lines, so the confirm dialog stays narrow: question, the steps as a flow, notes.</summary>
    public static string Confirm(BrowseMode mode, MessageSource source, string what)
    {
        var requeue = $"Requeue to {source.SendTarget}";
        string[] steps = mode switch
        {
            BrowseMode.Peek => [what, "PeekLock", requeue, "Mark complete"],
            BrowseMode.Locked => [what, requeue, "Mark complete"],
            _ => [what, requeue],
        };
        var lines = new List<string> { $"{Label(source.IsDlq)} {what}?", "", string.Join(" → ", steps) };
        if (mode == BrowseMode.Peek)
            lines.AddRange(["", "Messages ahead of them in the dead-letter queue are", "locked briefly and released (delivery count +1)."]);
        if (mode == BrowseMode.Received)
            lines.AddRange(["", "Receive-and-delete already removed the originals."]);
        if (source.IsSubscription)
            lines.AddRange(["", "Every subscription of the topic gets a copy."]);
        return string.Join('\n', lines);
    }
}
