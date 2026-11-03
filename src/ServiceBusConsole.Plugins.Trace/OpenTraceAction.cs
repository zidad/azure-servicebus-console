using System.Diagnostics.CodeAnalysis;

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
            ?? throw new InvalidOperationException($"Message #{context.Message.SequenceNumber} has no traceparent or Diagnostic-Id property");

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
        return (TraceLinks.Logs(resourceId, context.Settings[Tenant], trace, context.Message), $"Application Insights {Name(resourceId)}");
    }

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
