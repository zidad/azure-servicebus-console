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
    public Broker? InBroker => this switch
    {
        Broker broker => broker,
        Recent recent => recent.Target,
        _ => null,
    };

    public sealed record Connection(ConnectionInfo Value) : ExplorerNode
    {
        public override string Key => Value.Id ?? "emulator";
        public override string Name => Value.Name;
        public override string Icon => Value.IsEmulator ? Icons.Emulator : Terms.Connection.Icon;
        public override bool IsBranch => true;
        public override bool Matches(string filter) =>
            base.Matches(filter) || (Value.Id?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
    }

    public sealed record Broker(Connection Parent, BrokerInfo Info) : ExplorerNode
    {
        public override string Key => $"{Parent.Key}/{Info.Address}";
        public override string Name => Info.Name;
        public override string Icon => Terms.Broker.Icon;
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
    public sealed record Recent(Broker Target, DateTimeOffset Opened) : ExplorerNode
    {
        public override string Key => $"{RecentGroup.Instance.Key}/{Target.Key}";
        public override string Name => Target.Name;
        public override string Icon => Terms.Broker.Icon;
    }
}
