namespace ServiceBusConsole;

public enum SortDirection { None, Ascending, Descending }

public interface ITableSort
{
    SortDirection DirectionOf(string column);
    void Cycle(string column);
}

/// <summary>Click-to-sort state for a table: each click on a column cycles ascending, descending, off.</summary>
public sealed class TableSort<T>(Func<IEnumerable<T>, IEnumerable<T>> defaultOrder) : ITableSort
{
    private readonly Dictionary<string, Func<IEnumerable<T>, bool, IEnumerable<T>>> _columns = [];

    public string? Column { get; private set; }
    public SortDirection Direction { get; private set; }

    public TableSort<T> Add<TKey>(string column, Func<T, TKey> key, IComparer<TKey>? comparer = null)
    {
        _columns[column] = (items, descending) => descending
            ? items.OrderByDescending(key, comparer)
            : items.OrderBy(key, comparer);
        return this;
    }

    public SortDirection DirectionOf(string column) => column == Column ? Direction : SortDirection.None;

    public void Cycle(string column)
    {
        if (!_columns.ContainsKey(column)) throw new ArgumentException($"Unknown column '{column}'.", nameof(column));
        Direction = DirectionOf(column) switch
        {
            SortDirection.None => SortDirection.Ascending,
            SortDirection.Ascending => SortDirection.Descending,
            _ => SortDirection.None,
        };
        Column = Direction == SortDirection.None ? null : column;
    }

    // OrderBy is stable, so rows that tie on the column keep the default order.
    public List<T> Apply(IEnumerable<T> items)
    {
        var ordered = defaultOrder(items);
        return Column is { } c && _columns.TryGetValue(c, out var sort)
            ? [.. sort(ordered, Direction == SortDirection.Descending)]
            : [.. ordered];
    }
}
