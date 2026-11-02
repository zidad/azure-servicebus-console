namespace ServiceBusConsole.UI.Explorer;

/// <summary>A visible line of the explorer tree; <see cref="Prefix"/> holds the tree guides.</summary>
public abstract record ExplorerRow(string Key, string Prefix)
{
    public sealed record Item(ExplorerNode Node, string Prefix, bool Open) : ExplorerRow(Node.Key, Prefix);

    /// <summary>Loading or failure text under a branch.</summary>
    public sealed record Note(string ParentKey, string Prefix, string Text, bool IsError) : ExplorerRow($"{ParentKey}#note", Prefix);
}
