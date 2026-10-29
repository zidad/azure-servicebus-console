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
        string[][] notes =
        [
            mode == BrowseMode.Peek ? ["Messages ahead of them in the dead-letter queue are", "locked briefly and released (delivery count +1)."] : [],
            mode == BrowseMode.Received ? ["Receive-and-delete already removed the originals."] : [],
            source is MessageSource.Subscription ? ["Every subscription of the topic gets a copy."] : [],
        ];
        return string.Join('\n', [
            $"{Label(source.IsDlq)} {what}?", "", string.Join(" → ", steps),
            .. notes.Where(note => note.Length > 0).SelectMany(note => note.Prepend("")),
        ]);
    }
}
