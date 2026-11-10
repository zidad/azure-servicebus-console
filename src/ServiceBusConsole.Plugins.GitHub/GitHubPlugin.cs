using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.Plugins.GitHub.Resources;

namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>Creates GitHub issues from messages with the GitHub CLI and its existing login.</summary>
public sealed class GitHubPlugin : IPlugin
{
    public const string PluginId = "github";

    public string Id => PluginId;

    public string Name => Strings.GitHubPlugin_Name;

    public string Description => Strings.GitHubPlugin_Description;

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(IssueSettings.RepoKey, Strings.GitHubPlugin_RepoSetting),
        new(IssueSettings.LabelsKey, Strings.GitHubPlugin_LabelsSetting),
        new(IssueSettings.AssigneesKey, Strings.GitHubPlugin_AssigneesSetting),
        new(IssueSettings.TitleTemplateKey, Strings.FormatGitHubPlugin_TitleTemplateSetting("{entity} {subject} {messageId} {sequenceNumber} {namespace} {deadLetterReason}"), IssueSettings.DefaultTitleTemplate),
        new(IssueSettings.CliKey, Strings.GitHubPlugin_CliSetting, IssueSettings.DefaultCli),
        new(IssueSettings.IncludeContextKey, Strings.GitHubPlugin_IncludeContextSetting, "true"),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, CreateIssueAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[IssueSettings.CliKey]);
}
