namespace ServiceBusConsole.Messaging;

/// <summary>Label and confirm text for F10, shared by the browser and the detail page.</summary>
public static class RetryText
{
    /// <summary>Retry/Requeue is available in a dead-letter queue, and for received-and-deleted messages.</summary>
    public static bool CanRetry(BrowseMode mode, bool isDlq) => isDlq || mode == BrowseMode.Received;

    public static string Label(bool isDlq) => isDlq ? "Retry" : "Requeue";

    /// <summary>
    /// Short lines, so the confirm dialog stays narrow: question, the steps as a flow, notes. <paramref name="transactional"/>:
    /// the namespace moves held messages in transactions (received-and-deleted ones have no originals left to complete).
    /// </summary>
    public static string Confirm(BrowseMode mode, MessageSource source, string what, bool transactional)
    {
        var requeue = $"Requeue to {source.SendTarget}";
        string[] retry = transactional ? [$"Move to {source.SendTarget}"] : [requeue, "Mark complete"];
        string[] steps = mode switch
        {
            BrowseMode.Peek => [what, "PeekLock", .. retry],
            BrowseMode.Locked => [what, .. retry],
            _ => [what, requeue],
        };
        string[][] notes =
        [
            mode == BrowseMode.Peek ? ["Messages ahead of them in the dead-letter queue are", "locked briefly and released (delivery count +1)."] : [],
            mode == BrowseMode.Received ? ["Receive-and-delete already removed the originals."] : [],
            transactional && mode != BrowseMode.Received ? ["Up to 100 at a time move in one transaction:", "sent and completed together, or not at all."] : [],
            source is MessageSource.Subscription ? ["Every subscription of the topic gets a copy."] : [],
        ];
        return string.Join('\n', [
            $"{Label(source.IsDlq)} {what}?", "", string.Join(" → ", steps),
            .. notes.Where(note => note.Length > 0).SelectMany(note => note.Prepend("")),
        ]);
    }
}
