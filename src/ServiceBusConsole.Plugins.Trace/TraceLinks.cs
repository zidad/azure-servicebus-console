using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.Trace;

/// <summary>Where a trace opens: the Application Insights transaction or logs in the Azure portal, or a configured URL template.</summary>
public static partial class TraceLinks
{
    public const string Portal = "https://portal.azure.com/";

    /// <summary>From an hour before the message was enqueued to a day after.</summary>
    public static (DateTimeOffset Start, DateTimeOffset End) Window(MessageInfo message) =>
        (message.EnqueuedTime.ToUniversalTime().AddHours(-1), message.EnqueuedTime.ToUniversalTime().AddDays(1));

    public static string Query(TraceContext trace) =>
        $"""
        union *
        | where operation_Id == "{trace.TraceId}"
        | order by timestamp asc
        """;

    /// <summary>
    /// The record of the trace to open the transaction on: the request that handled the message (its parent is the message's
    /// span), else the earliest request, dependency or exception.
    /// </summary>
    public static string FirstItemQuery(TraceContext trace) =>
        $"""
        union requests, dependencies, exceptions
        | where operation_Id == "{trace.TraceId}"
        | extend handled = iff(operation_ParentId == "{trace.SpanId}", 0, 1)
        | order by handled asc, timestamp asc
        | take 1
        | project itemId, timestamp, itemType
        """;

    /// <summary>The end-to-end transaction view of Application Insights, on <paramref name="item"/>.</summary>
    public static string Transaction(string resourceId, string? tenant, TelemetryItem item)
    {
        var component = Json(w =>
        {
            w.WriteString("SubscriptionId", Segment(resourceId, "subscriptions"));
            w.WriteString("ResourceGroup", Segment(resourceId, "resourceGroups"));
            w.WriteString("Name", resourceId.TrimEnd('/').Split('/')[^1]);
            w.WriteNumber("LinkedApplicationType", 0);
            w.WriteString("ResourceId", Uri.EscapeDataString(resourceId));
            w.WriteString("ResourceType", Uri.EscapeDataString("microsoft.insights/components"));
            w.WriteBoolean("IsAzureFirst", false);
        });
        var dataModel = Json(w =>
        {
            w.WriteString("eventId", item.ItemId);
            w.WriteString("timestamp", item.Timestamp);
            w.WriteString("eventTable", item.Table);
        });
        return $"{Portal}#{(string.IsNullOrEmpty(tenant) ? "" : $"@{tenant}/")}view/AppInsightsExtension/DetailsV2Blade"
            + $"/ComponentId~/{Uri.EscapeDataString(component)}/DataModel~/{Uri.EscapeDataString(dataModel)}";
    }

    /// <summary>A Log Analytics query link (<c>q</c> is the gzipped, base64 query) on the Application Insights resource.</summary>
    public static string Logs(string resourceId, string? tenant, TraceContext trace, MessageInfo message)
    {
        var (start, end) = Window(message);
        return $"{Portal}#{(string.IsNullOrEmpty(tenant) ? "" : $"@{tenant}/")}blade/Microsoft_Azure_Monitoring_Logs/LogsBlade"
            + $"/resourceId/{Uri.EscapeDataString(resourceId)}"
            + $"/source/LogsBlade.AnalyticsShareLinkToQuery/q/{Uri.EscapeDataString(Compressed(Query(trace)))}"
            + $"/timespan/{Uri.EscapeDataString($"{Iso(start)}/{Iso(end)}")}";
    }

    /// <summary>
    /// <c>{traceId}</c>, <c>{spanId}</c>, <c>{start}</c>/<c>{end}</c> (ISO 8601), <c>{startMs}</c>/<c>{endMs}</c> (Unix milliseconds),
    /// then the <see cref="Template"/> placeholders; values are URL-escaped.
    /// </summary>
    public static string FromTemplate(string template, MessageActionContext context, TraceContext trace) =>
        Placeholder().Replace(template, match => Value(match.Value, context, trace) is { } value ? Uri.EscapeDataString(value) : match.Value);

    private static string? Value(string placeholder, MessageActionContext context, TraceContext trace)
    {
        var (start, end) = Window(context.Message);
        return placeholder.ToLowerInvariant() switch
        {
            "{traceid}" => trace.TraceId,
            "{spanid}" => trace.SpanId,
            "{start}" => Iso(start),
            "{end}" => Iso(end),
            "{startms}" => start.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            "{endms}" => end.ToUnixTimeMilliseconds().ToString(CultureInfo.InvariantCulture),
            _ when Template.Fill(placeholder, context) is var filled && filled != placeholder => filled,
            _ => null,
        };
    }

    private static string Segment(string resourceId, string name)
    {
        var parts = resourceId.Split('/', StringSplitOptions.RemoveEmptyEntries);
        var at = Array.FindIndex(parts, p => string.Equals(p, name, StringComparison.OrdinalIgnoreCase));
        return at >= 0 && at + 1 < parts.Length ? parts[at + 1] : throw new ArgumentException($"No {name} in {resourceId}", nameof(resourceId));
    }

    private static string Json(Action<Utf8JsonWriter> write)
    {
        using var buffer = new MemoryStream();
        using (var writer = new Utf8JsonWriter(buffer))
        {
            writer.WriteStartObject();
            write(writer);
            writer.WriteEndObject();
        }
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

        private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss.fff'Z'", CultureInfo.InvariantCulture);

    private static string Compressed(string text)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.Optimal)) gzip.Write(Encoding.UTF8.GetBytes(text));
        return Convert.ToBase64String(buffer.ToArray());
    }

    [GeneratedRegex(@"\{[A-Za-z]+\}")]
    private static partial Regex Placeholder();
}
