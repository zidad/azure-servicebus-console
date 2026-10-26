namespace ServiceBusConsole.Extensions;

public static class ListExtensions
{
    /// <summary>Replaces the first item matching <paramref name="match"/>, or adds <paramref name="item"/>; returns its index.</summary>
    public static int Upsert<T>(this List<T> list, T item, Predicate<T> match)
    {
        var idx = list.FindIndex(match);
        if (idx >= 0)
        {
            list[idx] = item;
            return idx;
        }
        list.Add(item);
        return list.Count - 1;
    }
}
