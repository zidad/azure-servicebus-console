using Microsoft.Extensions.Configuration;

[assembly: ServiceBusConsole.Plugins.ServiceBusConsolePlugin(typeof(ServiceBusConsole.Plugins.Trace.TracePlugin))]

namespace ServiceBusConsole.Plugins.Trace;

public sealed class TracePlugin : IPlugin
{
    public string Id => OpenTraceAction.Plugin;

    public string Name => "Trace";

    public string Description => "Opens the message's distributed trace in Application Insights or another trace viewer";

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(OpenTraceAction.AppInsights, "Application Insights resource id, or its name in the namespace's subscription; found with az when it is the only one"),
        new(OpenTraceAction.Tenant, "Azure AD tenant (id or domain) for the portal link, when it is not your default"),
        new(OpenTraceAction.Url, "URL template instead of the portal: {traceId}, {spanId}, {start}, {end}, {startMs}, {endMs}"),
        new(OpenTraceAction.Cli, "The Azure CLI, to find Application Insights", "az"),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, OpenTraceAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        settings[OpenTraceAction.Url] is { Length: > 0 } || OpenTraceAction.IsResourceId(settings[OpenTraceAction.AppInsights])
            ? null
            : environment.MissingCommand(settings[OpenTraceAction.Cli]);
}
