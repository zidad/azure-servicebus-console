namespace ServiceBusConsole.Abstractions;

/// <summary>What the user is looking at and what happened in the session, for plugins and LLM prompts.</summary>
public sealed record SessionSnapshot
{
    public required DateTimeOffset At { get; init; }
    public AzureSubscription? Subscription { get; init; }
    public NamespaceInfo? Namespace { get; init; }
    public MessageSource? Source { get; init; }

    /// <summary>How the messages were loaded: <c>Peek</c>, <c>Locked</c> (PeekLock) or <c>Received</c> (ReceiveAndDelete).</summary>
    public string? Mode { get; init; }

    /// <summary>The message open on the detail page, else the one under the cursor in the message list.</summary>
    public MessageInfo? Message { get; init; }

    /// <summary>Oldest first.</summary>
    public IReadOnlyList<ActivityEntry> Activity { get; init; } = [];

    public string ToMarkdown() => SessionMarkdown.Format(this);

    public string ToJson() => SessionJson.Format(this);
}
