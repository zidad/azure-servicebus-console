using Microsoft.Extensions.Logging;
using static ServiceBusConsole.UI.Explorer.ExplorerNode;

namespace ServiceBusConsole.UI.Explorer;

/// <summary>
/// The explorer's tree: subscriptions (open by default, so their namespaces list at once) › namespaces › the pages a
/// namespace has. Namespaces load the first time their subscription opens, cached first, then live. Kept for the run,
/// so the tree is as it was after visiting a page. Changed is raised on any thread.
/// </summary>
public sealed class ExplorerTree(
    SubscriptionCatalog catalog,
    INamespaceService namespaces,
    INamespaceOpener opener,
    ILogger<ExplorerTree> logger) : IDisposable
{
    private const string Root = "";

    private readonly Lock _sync = new();
    private readonly Dictionary<string, List<ExplorerNode>> _children = [];
    private readonly Dictionary<string, bool> _open = [];
    private readonly Dictionary<string, (string Text, bool IsError)> _notes = [];
    private readonly HashSet<string> _loaded = [];
    private readonly HashSet<string> _listing = [];
    private readonly CancellationTokenSource _cts = new();
    private bool _started;

    public event Action? Changed;

    public void EnsureStarted()
    {
        lock (_sync)
        {
            if (_started) return;
            _started = true;
        }
        catalog.Changed += SyncRoots;
        _ = catalog.EnsureLoadedAsync();
        SyncRoots();
    }

    public bool IsOpen(ExplorerNode node)
    {
        lock (_sync) return node.IsBranch && _open.GetValueOrDefault(node.Key, node is Subscription);
    }

    public void SetOpen(ExplorerNode node, bool open)
    {
        if (!node.IsBranch || IsOpen(node) == open && (!open || _loaded.Contains(node.Key))) return;
        lock (_sync) _open[node.Key] = open;
        if (open) Load(node);
        Changed?.Invoke();
    }

    /// <summary>Opens the path to <paramref name="ns"/>, so the tree shows where the app is.</summary>
    public void Reveal(AzureSubscription subscription, NamespaceInfo? ns)
    {
        var node = new Subscription(subscription);
        SetOpen(node, true);
        if (ns is not null) SetOpen(new Namespace(node, ns), true);
    }

    /// <summary>A subscription's namespaces listed so far; <c>Done</c> once its listing ended, also on failure.</summary>
    public Listing NamespacesOf(AzureSubscription subscription)
    {
        var key = new Subscription(subscription).Key;
        lock (_sync)
            return new([.. Children(key).OfType<Namespace>().Select(n => n.Info)], _loaded.Contains(key) && !_listing.Contains(key));
    }

    /// <summary>Lists a subscription's namespaces again, after a failure.</summary>
    public void Retry(string key)
    {
        ExplorerNode? node;
        lock (_sync)
        {
            node = Children(Root).FirstOrDefault(n => n.Key == key);
            if (node is not null) _loaded.Remove(key);
        }
        if (node is not null) Load(node);
    }

    /// <summary>Visible rows: open branches, or with a filter every loaded match with its ancestors (and what is open below it).</summary>
    public IReadOnlyList<ExplorerRow> Rows(string? filter)
    {
        lock (_sync)
        {
            var f = string.IsNullOrWhiteSpace(filter) ? null : filter.Trim();
            return [.. RowsOf(Root, "", f, ancestorMatched: false, depth: 0)];
        }
    }

    private IEnumerable<ExplorerRow> RowsOf(string key, string indent, string? filter, bool ancestorMatched, int depth)
    {
        var visible = Children(key)
            .Where(n => filter is null || ancestorMatched || n.Matches(filter) || HasMatchBelow(n, filter))
            .ToList();
        for (var i = 0; i < visible.Count; i++)
        {
            var node = visible[i];
            var last = i == visible.Count - 1;
            var matched = ancestorMatched || (filter is not null && node.Matches(filter));
            var open = node.IsBranch && (_open.GetValueOrDefault(node.Key, node is Subscription)
                || (filter is not null && !ancestorMatched && HasMatchBelow(node, filter)));
            yield return new ExplorerRow.Item(node, depth == 0 ? "" : indent + (last ? "└─ " : "├─ "), open);
            if (!open) continue;

            var childIndent = depth == 0 ? "" : indent + (last ? "   " : "│  ");
            foreach (var row in RowsOf(node.Key, childIndent, filter, matched, depth + 1)) yield return row;
            // "Discovering" only until the first (cached) namespace shows; errors always.
            if (_notes.TryGetValue(node.Key, out var note) && (note.IsError || !Children(node.Key).Any()) && (filter is null || matched))
                yield return new ExplorerRow.Note(node.Key, childIndent + "   ", note.Text, note.IsError);
        }
    }

    private bool HasMatchBelow(ExplorerNode node, string filter) =>
        Children(node.Key).Any(c => c.Matches(filter) || HasMatchBelow(c, filter));

    // Subscriptions keep the catalog's order (emulator first) and pages their own; namespaces go by name.
    private IEnumerable<ExplorerNode> Children(string key) =>
        !_children.TryGetValue(key, out var list) ? []
        : list.FirstOrDefault() is Namespace ? list.OrderBy(n => n.Name, StringComparer.CurrentCultureIgnoreCase)
        : list;

    // Subscriptions known to have no namespaces are left out, as on the connection page.
    private void SyncRoots()
    {
        List<ExplorerNode> roots;
        // Read under the lock: a sync that read an older list must not write after one that read a newer list.
        lock (_sync)
            _children[Root] = roots = [.. catalog.Subscriptions
                .Where(s => s.IsEmulator || catalog.HasNamespaces(s) != false)
                .Select(s => new Subscription(s))];
        foreach (var root in roots.Where(IsOpen)) Load(root);
        Changed?.Invoke();
    }

    private void Load(ExplorerNode node)
    {
        lock (_sync)
        {
            if (!_loaded.Add(node.Key)) return;
            // Marked before the task starts, so NamespacesOf never reports a listing that hasn't begun as done.
            if (node is Subscription)
            {
                _listing.Add(node.Key);
                _notes[node.Key] = ("Discovering namespaces…", false);
            }
        }
        // Off the renderer's context, like the catalog's loading.
        _ = Task.Run(() => node switch
        {
            Subscription s => LoadNamespacesAsync(s),
            Namespace ns => AddPagesAsync(ns),
            _ => Task.CompletedTask,
        });
    }

    private async Task LoadNamespacesAsync(Subscription node)
    {
        Changed?.Invoke();
        try
        {
            await foreach (var info in namespaces.GetNamespacesAsync(node.Value, _cts.Token))
            {
                lock (_sync) Add(node.Key, new Namespace(node, info));
                Changed?.Invoke();
            }
            lock (_sync) _notes.Remove(node.Key);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Listing namespaces of {Subscription} failed", node.Name);
            lock (_sync) _notes[node.Key] = ($"{ErrorText.Of(ex)}  (Enter to retry)", true);
        }
        finally
        {
            lock (_sync) _listing.Remove(node.Key);
        }
        Changed?.Invoke();
    }

    // Opening only composes the namespace's features; it connects on first use, which this never makes.
    private async Task AddPagesAsync(Namespace node)
    {
        await using var opened = opener.Open(node.Info);
        lock (_sync)
            foreach (var section in Section.Of(opened)) Add(node.Key, new Page(node, section));
        Changed?.Invoke();
    }

    private void Add(string parent, ExplorerNode node)
    {
        if (!_children.TryGetValue(parent, out var list)) _children[parent] = list = [];
        list.Upsert(node, n => n.Key == node.Key);
    }

    public readonly record struct Listing(IReadOnlyList<NamespaceInfo> Namespaces, bool Done);

    public void Dispose()
    {
        catalog.Changed -= SyncRoots;
        _cts.Cancel();
    }
}
