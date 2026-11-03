using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.Trace;

/// <summary>Where a trace opens: Application Insights logs in the Azure portal, or a configured URL template.</summary>
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
