namespace ServiceBusConsole.UI;

/// <summary>
/// The rows a list page shows, filtered and sorted, and the selected one: kept by key when the rows reload or re-sort.
/// Without <paramref name="matches"/> the list has no filter.
/// </summary>
public sealed class ListView<T>(Func<T, string> key, TableSort<T> sort, Func<T, string, bool>? matches = null) where T : class
{
    private List<T> _items = [];
    private string? _pendingKey;

    public IReadOnlyList<T> Items => _items;

    public ListFilter Filter { get; } = new();

    public bool Filterable => matches is not null;

    public int SelectedIndex { get; private set; }

    /// <summary>The selected row's key; before the first rows arrive, the one to select.</summary>
    public string? FocusedKey { get; private set; }

    public T? Focused => Items.ElementAtOrDefault(SelectedIndex);

    /// <summary>Selects <paramref name="rowKey"/> once a load brings it, unless the user selects another row first.</summary>
    public void Focus(string? rowKey) => FocusedKey = _pendingKey = rowKey;

    public void Select(int index)
    {
        _pendingKey = null;
        SelectedIndex = index;
        FocusedKey = KeyAt(index);
    }

    /// <summary>Applies the filter and the sort to <paramref name="all"/>, keeping the selected row.</summary>
    public void Update(IEnumerable<T> all)
    {
        var focused = FocusedKey;
        _items = sort.Apply(matches is null || Filter.Text is not { } text || string.IsNullOrWhiteSpace(text) ? all : all.Where(x => matches(x, text)));
        if (_pendingKey is { } pending && _items.Exists(x => key(x) == pending))
        {
            focused = pending;
            _pendingKey = null;
        }
        if (focused is not null && _items.FindIndex(x => key(x) == focused) is >= 0 and var index) SelectedIndex = index;
        FocusedKey = KeyAt(SelectedIndex);
    }

    /// <summary>A new filter: selects the first row.</summary>
    public void Refilter(IEnumerable<T> all)
    {
        SelectedIndex = 0;
        FocusedKey = null;
        Update(all);
    }

    /// <summary>After the selected row is gone: selects the row now in its place.</summary>
    public void Removed(IEnumerable<T> all)
    {
        FocusedKey = null;
        Update(all);
        Select(Math.Min(SelectedIndex, Math.Max(0, Items.Count - 1)));
    }

    private string? KeyAt(int index) => Items.ElementAtOrDefault(index) is { } item ? key(item) : null;
}
