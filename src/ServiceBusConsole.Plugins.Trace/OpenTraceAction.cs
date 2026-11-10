using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Text.Json;

namespace ServiceBusConsole.Plugins.Trace;

/// <summary>Opens the trace of the message's <see cref="TraceContext"/> in the default browser.</summary>
public sealed class OpenTraceAction : IMessageAction
{
    public const string Plugin = "trace";
    public const string AppInsights = "appInsights";
    public const string Tenant = "tenant";
    public const string Url = "url";
    public const string Cli = "cli";

    public string PluginId => Plugin;

    public string Id => "open";

    public string Label => "Open trace";

    public string Icon => "🔍";

    public bool CanRun(MessageActionContext context) => TraceContext.Of(context.Message) is not null;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var trace = TraceContext.Of(context.Message)
            ?? throw new InvalidOperationException($"Message #{context.Message.SequenceNumber} has no W3C trace context (traceparent) in its properties");

        var (url, where) = context.Settings[Url] is { Length: > 0 } template
            ? Templated(TraceLinks.FromTemplate(template, context, trace))
            : await PortalAsync(context, trace, ct);

        context.Progress.Report("Opening the browser…");
        await context.Host.OpenAsync(url, ct);
        return new ActionResult($"Opened trace {trace.TraceId} in {where}", new Uri(url));
    }

    public static bool IsResourceId([NotNullWhen(true)] string? value) => value?.StartsWith("/subscriptions/", StringComparison.OrdinalIgnoreCase) == true;

    private static (string Url, string Where) Templated(string url) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
            ? (url, uri.Host is { Length: > 0 } host ? host : url)
            : throw new InvalidOperationException($"plugins.{Plugin}.{Url} is not an absolute URL: {url}");

    private static async Task<(string Url, string Where)> PortalAsync(MessageActionContext context, TraceContext trace, CancellationToken ct)
    {
        var resourceId = await ResourceIdAsync(context, ct);
        var tenant = context.Settings[Tenant];
        var where = $"Application Insights {Name(resourceId)}";
        return await FirstItemAsync(context, resourceId, trace, ct) is { } item
            ? (TraceLinks.Transaction(resourceId, tenant, item), where)
            : (TraceLinks.Logs(resourceId, tenant, trace, context.Message), $"{where} logs");
    }

    /// <summary>The record to open the transaction view on; null when az can't query or the trace has none, so the logs open instead.</summary>
    private static async Task<TelemetryItem?> FirstItemAsync(MessageActionContext context, string resourceId, TraceContext trace, CancellationToken ct)
    {
        context.Progress.Report("Finding the trace…");
        var (start, end) = TraceLinks.Window(context.Message);
        try
        {
            var result = await context.Host.RunProcessAsync(new ProcessSpec(context.RequiredSetting(Cli),
                ["monitor", "app-insights", "query", "--ids", resourceId, "--start-time", Iso(start), "--end-time", Iso(end),
                 "--analytics-query", TraceLinks.FirstItemQuery(trace), "--output", "json"]), ct);
            return result.ExitCode == 0 ? FirstItem(result.StandardOutput) : null;
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return null;
        }
    }

    private static TelemetryItem? FirstItem(string json)
    {
        try
        {
            using var document = JsonDocument.Parse(json);
            var rows = document.RootElement.GetProperty("tables")[0].GetProperty("rows");
            return rows.GetArrayLength() > 0 && rows[0] is var row
                && row[0].GetString() is { } itemId && row[1].GetString() is { } timestamp && row[2].GetString() is { } type
                ? new TelemetryItem(itemId, timestamp, Table(type))
                : null;
        }
        catch (Exception ex) when (ex is JsonException or KeyNotFoundException or IndexOutOfRangeException or InvalidOperationException)
        {
            return null;
        }
    }

    private static string Table(string itemType) => itemType switch
    {
        "request" => "requests",
        "dependency" => "dependencies",
        "exception" => "exceptions",
        _ => itemType + "s",
    };

    private static string Iso(DateTimeOffset time) => time.UtcDateTime.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);

    private static async Task<string> ResourceIdAsync(MessageActionContext context, CancellationToken ct)
    {
        var setting = context.Settings[AppInsights];
        if (IsResourceId(setting)) return setting;

        var subscription = context.Session.Subscription?.Id
            ?? throw new InvalidOperationException(
                $"Set where traces are: azsb config set plugins.{Plugin}.{AppInsights} <resource id>, or plugins.{Plugin}.{Url} <template>");
        context.Progress.Report("Finding Application Insights…");
        var cli = context.RequiredSetting(Cli);
        var found = (await context.Host.RunProcessAsync(
                new ProcessSpec(cli, ["resource", "list", "--subscription", subscription, "--resource-type", "microsoft.insights/components", "--query", "[].id", "--output", "tsv"]), ct))
            .EnsureSuccess(cli).StandardOutput
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(id => string.IsNullOrEmpty(setting) || string.Equals(Name(id), setting, StringComparison.OrdinalIgnoreCase))
            .ToList();
        return found switch
        {
            [var only] => only,
            [] when string.IsNullOrEmpty(setting) => throw new InvalidOperationException(
                $"No Application Insights in this subscription; set one: azsb config set plugins.{Plugin}.{AppInsights} <resource id>"),
            [] => throw new InvalidOperationException($"Application Insights {setting} not found in this subscription; set its resource id instead"),
            _ => throw new InvalidOperationException(
                $"Choose an Application Insights ({string.Join(", ", found.Select(Name))}): azsb config set plugins.{Plugin}.{AppInsights} <name>"),
        };
    }

    private static string Name(string resourceId) => resourceId.TrimEnd('/').Split('/')[^1];
}
