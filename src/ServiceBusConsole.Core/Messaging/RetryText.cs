namespace ServiceBusConsole.Core.Messaging;

/// <summary>Label and confirm text for F10 (retry, requeue, resend), shared by the browser and the detail page.</summary>
public static class RetryText
{
    /// <summary>Retry/Requeue is available in a dead-letter queue, and for received-and-deleted messages.</summary>
    public static bool CanRetry(BrowseMode mode, bool isDlq) => isDlq || mode == BrowseMode.Received;

    public static string Label(bool isDlq) => isDlq ? MessagingStrings.RetryText_Retry : MessagingStrings.RetryText_Requeue;

    public static string Question(MessageSource source, string what) =>
        source.IsDlq ? MessagingStrings.FormatRetryText_RetryQuestion(what) : MessagingStrings.FormatRetryText_RequeueQuestion(what);

    public static string ResendQuestion(string what) => MessagingStrings.FormatRetryText_ResendQuestion(what);

    /// <summary>
    /// From / To / How / Note, in short lines so the confirm dialog stays narrow; How starts with the progress steps.
    /// <paramref name="transactional"/>: the namespace moves held messages in transactions (received-and-deleted ones
    /// have no originals left to complete).
    /// </summary>
    public static IReadOnlyList<DialogField> Fields(BrowseMode mode, MessageSource source, bool transactional)
    {
        var requeue = MessagingStrings.FormatRetryText_RequeueTo(source.SendTarget);
        string[] retry = transactional ? [MessagingStrings.FormatRetryText_MoveTo(source.SendTarget)] : [requeue, MessagingStrings.RetryText_MarkComplete];
        string[] steps = mode switch
        {
            BrowseMode.Peek => [Strings.PeekLock, .. retry],
            BrowseMode.Locked => retry,
            _ => [requeue],
        };
        string?[] how =
        [
            string.Join(" → ", steps),
            mode == BrowseMode.Received ? null : transactional ? MessagingStrings.RetryText_TransactionNote : MessagingStrings.RetryText_AtLeastOnceNote,
        ];
        return Grid(source, string.Join('\n', how.OfType<string>()),
        [
            mode == BrowseMode.Peek ? MessagingStrings.RetryText_PeekNote : null,
            mode == BrowseMode.Received ? MessagingStrings.RetryText_ReceivedNote : null,
            TopicNote(source),
        ]);
    }

    /// <summary>From / To / How / Note for Resend on an active queue or subscription: copies go out, the originals stay.</summary>
    public static IReadOnlyList<DialogField> ResendFields(MessageSource source) =>
        Grid(source, MessagingStrings.FormatRetryText_SendCopiesTo(source.SendTarget),
            [MessagingStrings.FormatRetryText_OriginalsStay(source.DisplayName), TopicNote(source)]);

    private static string? TopicNote(MessageSource source) => source is MessageSource.Subscription ? MessagingStrings.RetryText_SubscriptionNote : null;

    private static IReadOnlyList<DialogField> Grid(MessageSource source, string how, IEnumerable<string?> notes) =>
    [
        new(MessagingStrings.RetryText_From, source.DisplayName),
        new(MessagingStrings.RetryText_To, source.SendTarget),
        new(MessagingStrings.RetryText_How, how),
        .. notes.OfType<string>().Select((note, i) => new DialogField(i == 0 ? MessagingStrings.RetryText_Note : "", note, Warning: true)),
    ];
}
