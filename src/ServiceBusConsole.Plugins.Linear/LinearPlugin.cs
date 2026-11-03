using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.Plugins.Linear;

[assembly: ServiceBusConsolePlugin(typeof(LinearPlugin))]

namespace ServiceBusConsole.Plugins.Linear;

public sealed class LinearPlugin : IPlugin
{
    public const string PluginId = "linear";
    public const string ApiKeyVariable = "LINEAR_API_KEY";

    public string Id => PluginId;

    public string Name => "Linear";

    public string Description => "Creates a Linear issue from a message";

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(LinearSettings.ApiKey, $"Linear personal API key (Settings > Security & access); else {ApiKeyVariable}", IsSecret: true),
        new(LinearSettings.Team, "Key of the team the issue goes to, e.g. ENG"),
        new(LinearSettings.Labels, "Comma-separated label names to add, e.g. bug,servicebus"),
        new(LinearSettings.TitleTemplate, "Issue title: {entity}, {subject}, {messageId}, {sequenceNumber}, {namespace}, {deadLetterReason}", LinearSettings.DefaultTitleTemplate),
        new(LinearSettings.IncludeContext, "Adds where the message came from and the session's recent activity to the description", "true"),
    ];

    public void ConfigureServices(IServiceCollection services) =>
        services
            .AddSingleton(_ => new LinearClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }))
            .AddSingleton<IMessageAction>(sp => new CreateIssueAction(sp.GetRequiredService<LinearClient>(), Environment.GetEnvironmentVariable));

    // No CLI to look for: the API key is what it needs.
    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        settings[LinearSettings.ApiKey] is { Length: > 0 } || environment.Variable(ApiKeyVariable) is { Length: > 0 }
            ? null
            : $"no API key; set plugins.{PluginId}.{LinearSettings.ApiKey} or {ApiKeyVariable}";
}
