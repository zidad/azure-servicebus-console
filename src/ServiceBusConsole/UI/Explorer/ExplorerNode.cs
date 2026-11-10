namespace ServiceBusConsole.UI.Explorer;

/// <summary>A node of the explorer tree: the recent group › recent namespaces, then subscription › namespace; a namespace opens on its queues tab.</summary>
public abstract record ExplorerNode
{
    private ExplorerNode() { }

    /// <summary>Unique within the tree: the path of names from the root.</summary>
    public abstract string Key { get; }

    public abstract string Name { get; }

    public abstract string Icon { get; }

    public virtual bool IsBranch => false;

    public virtual bool Matches(string filter) => Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>The namespace this node is, if any.</summary>
    public Namespace? InNamespace => this switch
    {
        Namespace ns => ns,
        Recent recent => recent.Target,
        _ => null,
    };

    public sealed record Subscription(AzureSubscription Value) : ExplorerNode
    {
        public override string Key => Value.Id ?? "emulator";
        public override string Name => Value.Name;
        public override string Icon => Value.IsEmulator ? Icons.Emulator : Icons.AzureSubscription;
        public override bool IsBranch => true;
        public override bool Matches(string filter) =>
            base.Matches(filter) || (Value.Id?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public sealed record Namespace(Subscription Parent, NamespaceInfo Info) : ExplorerNode
    {
        public override string Key => $"{Parent.Key}/{Info.FullyQualifiedNamespace}";
        public override string Name => Info.Name;
        public override string Icon => Icons.Namespace;
    }

    /// <summary>The namespaces opened last, above the subscriptions.</summary>
    public sealed record RecentGroup : ExplorerNode
    {
        public static RecentGroup Instance { get; } = new();

        private RecentGroup() { }

        public override string Key => "recent";
        public override string Name => UIStrings.ExplorerTree_Recent;
        public override string Icon => Icons.Recent;
        public override bool IsBranch => true;
    }

    /// <summary>A namespace opened at <paramref name="Opened"/>; it stands for <paramref name="Target"/>, its row in the tree.</summary>
    public sealed record Recent(Namespace Target, DateTimeOffset Opened) : ExplorerNode
    {
        public override string Key => $"{RecentGroup.Instance.Key}/{Target.Key}";
        public override string Name => Target.Name;
        public override string Icon => Icons.Namespace;
    }
}
