namespace ServiceBusConsole.Messaging;

/// <summary>Keeps the one message browser in use, so its locks survive a trip to the detail page.</summary>
public sealed class MessageBrowserStore(ServiceBusConnection connection) : IAsyncDisposable
{
    public MessageBrowser? Current { get; private set; }

    /// <summary>Returns the browser for <paramref name="source"/> in the current namespace; <c>IsNew</c> is false when it
    /// was already open. Opening another source releases the previous one's locks in the background.</summary>
    public (MessageBrowser Browser, bool IsNew) Open(MessageSource source)
    {
        var ns = connection.Namespace;
        if (Current is { } current && current.Source == source && current.Namespace == ns) return (current, false);
        _ = CloseAsync();
        Current = new MessageBrowser(source, ns);
        return (Current, true);
    }

    public async Task CloseAsync()
    {
        if (Current is null) return;
        var current = Current;
        Current = null;
        await current.DisposeAsync();
    }

    public ValueTask DisposeAsync() => new(CloseAsync());
}
