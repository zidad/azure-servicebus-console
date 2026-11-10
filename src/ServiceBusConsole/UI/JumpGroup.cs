namespace ServiceBusConsole.UI;

/// <summary>The matches of one kind, in rank order.</summary>
public sealed record JumpGroup(JumpKind Kind, IReadOnlyList<JumpTarget> Targets)
{
    public string Title => Kind switch
    {
        JumpKind.Queue => Strings.Queues,
        JumpKind.Topic => Strings.Topics,
        JumpKind.Subscription => Strings.Subscriptions,
        _ => UIStrings.JumpGroup_OtherNamespaces,
    };
}
