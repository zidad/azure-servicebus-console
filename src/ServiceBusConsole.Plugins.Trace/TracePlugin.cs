using Microsoft.Extensions.Configuration;
using ServiceBusConsole.Plugins.Trace.Resources;

namespace ServiceBusConsole.Plugins.Trace;

public sealed class TracePlugin : IPlugin
{
    public string Id => OpenTraceAction.Plugin;

    public string Name => Strings.TracePlugin_Name;

    public string Description => Strings.TracePlugin_Description;

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(OpenTraceAction.AppInsights, Strings.TracePlugin_AppInsightsSetting),
        new(OpenTraceAction.Tenant, Strings.TracePlugin_TenantSetting),
        new(OpenTraceAction.Url, Strings.FormatTracePlugin_UrlSetting("{traceId}, {spanId}, {start}, {end}, {startMs}, {endMs}")),
        new(OpenTraceAction.Cli, Strings.TracePlugin_CliSetting, "az"),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, OpenTraceAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        settings[OpenTraceAction.Url] is { Length: > 0 } || OpenTraceAction.IsResourceId(settings[OpenTraceAction.AppInsights])
            ? null
            : environment.MissingCommand(settings[OpenTraceAction.Cli]);
}
