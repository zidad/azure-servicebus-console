namespace ServiceBusConsole.Messaging;

/// <summary>
/// Keeps the one message browser in use, so its locks survive a trip to the detail page. Records each browser's loads
/// and operations in the <see cref="ActivityLog"/>.
/// </summary>
public sealed class MessageBrowserStore(ServiceBusConnection connection, ActivityLog activity) : IAsyncDisposable
{
    public MessageBrowser? Current { get; private set; }

    /// <summary>Returns the browser for <paramref name="source"/> in the current namespace; <c>IsNew</c> is false when it
    /// was already open. Opening another source releases the previous one's locks in the background.</summary>
    public (MessageBrowser Browser, bool IsNew) Open(MessageSource source)
    {
        var ns = connection.Namespace;
        if (Current is { } current && current.Source == source && current.Namespace == ns) return (current, false);
        _ = CloseAsync();
        var browser = new MessageBrowser(source, ns);
        browser.Ended += op => Record(browser, op);
        Current = browser;
        return (browser, true);
    }

    private void Record(MessageBrowser browser, OperationProgress op)
    {
        var (kind, text) = op switch
        {
            { Result: { } result } => (op.Outcome == OperationOutcome.Failed ? ActivityKind.Error : ActivityKind.Operation, $"{op.Title}: {result}"),
            _ => (ActivityKind.Loaded, Loaded(browser.Mode, browser.Messages.Count) + (op.Cancelled ? MessagingStrings.MessageBrowserStore_Cancelled : "")),
        };
        activity.Record(kind, text, browser.Source);
    }

    private static string Loaded(BrowseMode mode, int count) => mode switch
    {
        BrowseMode.Locked => MessagingStrings.FormatMessageBrowserStore_ReceivedWithPeekLock(count.ToString("N0")),
        BrowseMode.Received => MessagingStrings.FormatMessageBrowserStore_ReceivedAndDeleted(count.ToString("N0")),
        _ => MessagingStrings.FormatMessageBrowserStore_Peeked(count.ToString("N0")),
    };

    public async Task CloseAsync()
    {
        if (Current is null) return;
        var current = Current;
        Current = null;
        await current.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(CloseAsync());
}
