namespace ServiceBusConsole.UI;

/// <summary>
/// Ranks jump targets for a query: names starting with it first, then containing it, then holding its letters in order
/// ("pmt" finds "payments"); within each, dead letters first, then by name. An empty query lists dead letters first.
/// </summary>
public static class JumpIndex
{
    public static IReadOnlyList<JumpTarget> Search(string? query, IEnumerable<JumpTarget> targets, int max = 12)
    {
        var q = query?.Trim() ?? "";
        return [.. targets
            .Select(t => (Target: t, Rank: Rank(t.Name, q)))
            .Where(r => r.Rank is not null)
            .OrderBy(r => r.Rank)
            .ThenByDescending(r => r.Target.DeadLetters)
            .ThenBy(r => r.Target.Name, StringComparer.OrdinalIgnoreCase)
            .Take(max)
            .Select(r => r.Target)];
    }

    /// <summary>0 prefix, 1 contains, 2 letters in order, null no match; any name matches an empty query.</summary>
    public static int? Rank(string name, string query) => query.Length == 0 ? 0
        : name.StartsWith(query, StringComparison.OrdinalIgnoreCase) ? 0
        : name.Contains(query, StringComparison.OrdinalIgnoreCase) ? 1
        : InOrder(name, query) ? 2
        : null;

    private static bool InOrder(string name, string query)
    {
        var at = 0;
        foreach (var c in query)
        {
            at = name.IndexOf(c.ToString(), at, StringComparison.OrdinalIgnoreCase);
            if (at < 0) return false;
            at++;
        }
        return true;
    }
}
