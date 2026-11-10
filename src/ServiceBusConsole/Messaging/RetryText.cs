namespace ServiceBusConsole.Messaging;

/// <summary>Label and confirm text for F10, shared by the browser and the detail page.</summary>
public static class RetryText
{
    /// <summary>Retry/Requeue is available in a dead-letter queue, and for received-and-deleted messages.</summary>
    public static bool CanRetry(BrowseMode mode, bool isDlq) => isDlq || mode == BrowseMode.Received;

    public static string Label(bool isDlq) => isDlq ? MessagingStrings.RetryText_Retry : MessagingStrings.RetryText_Requeue;

    /// <summary>
    /// Short lines, so the confirm dialog stays narrow: question, the steps as a flow, notes. <paramref name="transactional"/>:
    /// the namespace moves held messages in transactions (received-and-deleted ones have no originals left to complete).
    /// </summary>
    public static string Confirm(BrowseMode mode, MessageSource source, string what, bool transactional)
    {
        var requeue = MessagingStrings.FormatRetryText_RequeueTo(source.SendTarget);
        string[] retry = transactional ? [MessagingStrings.FormatRetryText_MoveTo(source.SendTarget)] : [requeue, MessagingStrings.RetryText_MarkComplete];
        string[] steps = mode switch
        {
            BrowseMode.Peek => [what, Strings.PeekLock, .. retry],
            BrowseMode.Locked => [what, .. retry],
            _ => [what, requeue],
        };
        string?[] notes =
        [
            mode == BrowseMode.Peek ? MessagingStrings.RetryText_PeekNote : null,
            mode == BrowseMode.Received ? MessagingStrings.RetryText_ReceivedNote : null,
            transactional && mode != BrowseMode.Received ? MessagingStrings.RetryText_TransactionNote : null,
            source is MessageSource.Subscription ? MessagingStrings.RetryText_SubscriptionNote : null,
        ];
        var question = source.IsDlq ? MessagingStrings.FormatRetryText_RetryQuestion(what) : MessagingStrings.FormatRetryText_RequeueQuestion(what);
        return string.Join('\n', [
            question, "", string.Join(" → ", steps),
            .. notes.OfType<string>().SelectMany(note => new[] { "", note }),
        ]);
    }
}
