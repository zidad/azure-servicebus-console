using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.Trace;

/// <summary>
/// The W3C trace context a producer put on the message. Known properties first: <c>traceparent</c>, <c>Diagnostic-Id</c>
/// (Azure SDK), <c>MT-Activity-Id</c> (MassTransit), <c>parent-id</c> (Wolverine); then any property whose value is one.
/// </summary>
public sealed partial record TraceContext(string TraceId, string SpanId)
{
    public static IReadOnlyList<string> PropertyNames { get; } = ["traceparent", "Diagnostic-Id", "MT-Activity-Id", "parent-id"];

    public static TraceContext? Of(MessageInfo message) =>
        PropertyNames
            .SelectMany(name => message.Properties.Where(p => string.Equals(p.Key, name, StringComparison.OrdinalIgnoreCase)))
            .Concat(message.Properties)
            .Select(p => Parse(p.Value))
            .FirstOrDefault(context => context is not null);

    /// <summary><c>00-&lt;trace id&gt;-&lt;span id&gt;-&lt;flags&gt;</c>; null for anything else, e.g. the old hierarchical <c>|id.</c> format.</summary>
    public static TraceContext? Parse(string value) =>
        TraceParent().Match(value.Trim()) is { Success: true } match && match.Groups[1].Value.Any(c => c != '0')
            ? new(match.Groups[1].Value.ToLowerInvariant(), match.Groups[2].Value.ToLowerInvariant())
            : null;

    [GeneratedRegex("^[0-9a-f]{2}-([0-9a-f]{32})-([0-9a-f]{16})-[0-9a-f]{2}$", RegexOptions.IgnoreCase)]
    private static partial Regex TraceParent();
}
