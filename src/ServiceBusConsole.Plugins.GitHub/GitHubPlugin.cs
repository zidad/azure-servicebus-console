using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

[assembly: ServiceBusConsolePlugin(typeof(ServiceBusConsole.Plugins.GitHub.GitHubPlugin))]

namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>Creates GitHub issues from messages with the GitHub CLI and its existing login.</summary>
public sealed class GitHubPlugin : IPlugin
{
    public const string PluginId = "github";

    public string Id => PluginId;

    public string Name => "GitHub";

    public string Description => "Creates a GitHub issue from a message with the GitHub CLI (gh)";

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(IssueSettings.RepoKey, "Repository to create issues in, owner/name"),
        new(IssueSettings.LabelsKey, "Labels to add, comma-separated"),
        new(IssueSettings.AssigneesKey, "Users to assign, comma-separated; @me assigns yourself"),
        new(IssueSettings.TitleTemplateKey, "Issue title: {entity} {subject} {messageId} {sequenceNumber} {namespace} {deadLetterReason}", IssueSettings.DefaultTitleTemplate),
        new(IssueSettings.CliKey, "GitHub CLI to run, a name on the PATH or a path", IssueSettings.DefaultCli),
        new(IssueSettings.IncludeContextKey, "Add where the message came from and the session's recent activity", "true"),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, CreateIssueAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[IssueSettings.CliKey]);
}
