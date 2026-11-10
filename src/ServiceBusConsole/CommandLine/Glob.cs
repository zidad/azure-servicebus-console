using System.Text.RegularExpressions;

namespace ServiceBusConsole.CommandLine;

/// <summary>A name pattern with <c>*</c> (any run) and <c>?</c> (one character), ignoring case.</summary>
public sealed class Glob(string pattern)
{
    private readonly Regex _regex = new(
        "^" + Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".") + "$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

    public string Pattern { get; } = pattern;

    public bool IsPattern => Pattern.Contains('*') || Pattern.Contains('?');

    public bool Matches(string name) => _regex.IsMatch(name);

    /// <summary>A comma-separated list of patterns, as a setting holds them; empty entries are skipped.</summary>
    public static IReadOnlyList<Glob> List(string? patterns) =>
        [.. (patterns ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(p => new Glob(p))];
}
