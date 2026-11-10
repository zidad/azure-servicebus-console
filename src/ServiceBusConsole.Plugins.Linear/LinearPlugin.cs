using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.Plugins.Linear.Resources;

namespace ServiceBusConsole.Plugins.Linear;

public sealed class LinearPlugin : IPlugin
{
    public const string PluginId = "linear";
    public const string ApiKeyVariable = "LINEAR_API_KEY";

    public string Id => PluginId;

    public string Name => Strings.LinearPlugin_Name;

    public string Description => Strings.LinearPlugin_Description;

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(LinearSettings.ApiKey, Strings.FormatLinearPlugin_ApiKeySetting(ApiKeyVariable), IsSecret: true),
        new(LinearSettings.Team, Strings.LinearPlugin_TeamSetting),
        new(LinearSettings.Labels, Strings.LinearPlugin_LabelsSetting),
        new(LinearSettings.TitleTemplate,
            Strings.FormatLinearPlugin_TitleTemplateSetting("{entity}, {subject}, {messageId}, {sequenceNumber}, {namespace}, {deadLetterReason}"),
            LinearSettings.DefaultTitleTemplate),
        new(LinearSettings.IncludeContext, Strings.LinearPlugin_IncludeContextSetting, "true"),
    ];

    public void ConfigureServices(IServiceCollection services) =>
        services
            .AddSingleton(_ => new LinearClient(new HttpClient { Timeout = TimeSpan.FromSeconds(30) }))
            .AddSingleton<IMessageAction>(sp => new CreateIssueAction(sp.GetRequiredService<LinearClient>(), Environment.GetEnvironmentVariable));

    // No CLI to look for: the API key is what it needs.
    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        settings[LinearSettings.ApiKey] is { Length: > 0 } || environment.Variable(ApiKeyVariable) is { Length: > 0 }
            ? null
            : Strings.FormatLinearPlugin_NoApiKey(PluginId, LinearSettings.ApiKey, ApiKeyVariable);
}
