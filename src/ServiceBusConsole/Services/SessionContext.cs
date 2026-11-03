namespace ServiceBusConsole.Services;

/// <summary>
/// What the user is looking at (from <see cref="NavigationState"/>, <see cref="ServiceBusConnection"/> and
/// <see cref="MessageBrowserStore"/>) and what happened (<see cref="ActivityLog"/>), as a <see cref="SessionSnapshot"/>.
/// </summary>
public sealed class SessionContext(NavigationState navState, ServiceBusConnection connection, MessageBrowserStore store, ActivityLog log)
{
    private const string DetailPath = "message";

    private readonly Lock _lock = new();
    private string _path = "";
    private NamespaceInfo? _namespace;

    public ActivityLog Activity => log;

    public ActivityEntry Record(ActivityKind kind, string text, MessageSource? source = null) => log.Record(kind, text, source);

    /// <summary>Records arriving at <paramref name="path"/> (base-relative), after a namespace switch if there was one.</summary>
    public void Navigated(string path)
    {
        var page = path.Trim('/');
        var ns = connection.Current?.Info;
        bool switched;
        lock (_lock)
        {
            _path = page;
            switched = ns != _namespace;
            _namespace = ns;
        }
        if (switched && ns is not null) log.Record(ActivityKind.Navigated, $"Connected to namespace {ns.FullyQualifiedNamespace}");
        if (page == DetailPath && navState.Message is { } message)
            log.Record(ActivityKind.Navigated, $"Opened message #{message.SequenceNumber}", navState.MessageSource);
        else log.Record(ActivityKind.Navigated, $"Opened /{page}");
    }

    public SessionSnapshot Snapshot()
    {
        string page;
        lock (_lock) page = _path;
        var ns = connection.Current;
        var onDetail = page == DetailPath;
        // The store keeps a browser while its detail page is open, and until the next browser page or the connection
        // page replaces it; only one in view counts.
        var browser = store.Current is { } b && b.Namespace == ns && (onDetail || IsBrowserPage(page)) ? b : null;
        var source = onDetail ? navState.MessageSource : browser?.Source;
        return new SessionSnapshot
        {
            At = log.Time.GetUtcNow(),
            Subscription = navState.AzureSubscription,
            Namespace = ns?.Info,
            Source = source,
            Mode = browser is not null && browser.Source == source ? browser.Mode.ToString() : null,
            Message = onDetail ? navState.Message : browser?.Focused,
            Activity = log.Entries,
        };
    }

    private static bool IsBrowserPage(string page) => page.StartsWith("queues/", StringComparison.Ordinal) || page == "subscription-browser";
}
