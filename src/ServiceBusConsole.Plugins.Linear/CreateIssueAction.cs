namespace ServiceBusConsole.Plugins.Linear;

/// <param name="environment">Reads an environment variable; tests pass their own.</param>
internal sealed class CreateIssueAction(LinearClient linear, Func<string, string?> environment) : IMessageAction
{
    public string PluginId => LinearPlugin.PluginId;

    public string Id => "create-issue";

    public string Label => "Create Linear issue";

    public string Icon => "📋";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var apiKey = ApiKey(context);
        var team = context.RequiredSetting(LinearSettings.Team).Trim();
        var labels = Labels(context);

        context.Progress.Report($"Finding team {team}…");
        var teamId = await linear.TeamIdAsync(apiKey, team, ct);

        if (labels.Count > 0) context.Progress.Report($"Finding labels {string.Join(", ", labels)}…");
        var labelIds = await linear.LabelIdsAsync(apiKey, teamId, labels, ct);

        context.Progress.Report("Creating issue…");
        var issue = new NewIssue(
            teamId,
            IssueText.Title(context, context.Settings[LinearSettings.TitleTemplate] ?? LinearSettings.DefaultTitleTemplate),
            IssueText.Description(context, IncludeContext(context)),
            labelIds);
        var created = await linear.CreateIssueAsync(apiKey, issue, ct);
        return new ActionResult($"Created {created.Identifier}", Link: created.Url);
    }

    private string ApiKey(MessageActionContext context) =>
        context.Settings[LinearSettings.ApiKey] is { Length: > 0 } configured ? configured.Trim()
        : environment(LinearPlugin.ApiKeyVariable) is { Length: > 0 } variable ? variable.Trim()
        : throw new InvalidOperationException(
            $"Set a Linear API key first: azsb config set plugins.{LinearPlugin.PluginId}.{LinearSettings.ApiKey} <key>, or set {LinearPlugin.ApiKeyVariable}");

    private static IReadOnlyList<string> Labels(MessageActionContext context) =>
        [.. (context.Settings[LinearSettings.Labels] ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static bool IncludeContext(MessageActionContext context) =>
        !bool.TryParse(context.Settings[LinearSettings.IncludeContext], out var include) || include;
}
