namespace ServiceBusConsole.UI.Explorer;

/// <summary>A node of the explorer tree: subscription › namespace › its pages (queues, topics, all subscriptions).</summary>
public abstract record ExplorerNode
{
    private ExplorerNode() { }

    /// <summary>Unique within the tree: the path of names from the root.</summary>
    public abstract string Key { get; }

    public abstract string Name { get; }

    public abstract string Icon { get; }

    public virtual bool IsBranch => false;

    public virtual bool Matches(string filter) => Name.Contains(filter, StringComparison.CurrentCultureIgnoreCase);

    /// <summary>The namespace this node is or belongs to.</summary>
    public Namespace? InNamespace => this switch
    {
        Namespace ns => ns,
        Page p => p.Parent,
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
        public override bool IsBranch => true;
    }

    /// <summary>A namespace's list page; never matches a filter, it shows under a matching namespace.</summary>
    public sealed record Page(Namespace Parent, Section Section) : ExplorerNode
    {
        public override string Key => $"{Parent.Key}{Section.Route}";
        public override string Name => Section.Name;
        public override string Icon => Section.Icon;
        public override bool Matches(string filter) => false;
    }
}
