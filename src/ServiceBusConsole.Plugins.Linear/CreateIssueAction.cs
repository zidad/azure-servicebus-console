using ServiceBusConsole.Plugins.Linear.Resources;

namespace ServiceBusConsole.Plugins.Linear;

/// <param name="environment">Reads an environment variable; tests pass their own.</param>
internal sealed class CreateIssueAction(LinearClient linear, Func<string, string?> environment) : IMessageAction
{
    public string PluginId => LinearPlugin.PluginId;

    public string Id => "create-issue";

    public string Label => Strings.CreateIssueAction_Label;

    public string Icon => "📋";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var apiKey = ApiKey(context);
        var team = context.RequiredSetting(LinearSettings.Team).Trim();
        var labels = Labels(context);

        context.Progress.Report(Strings.FormatCreateIssueAction_FindingTeam(team));
        var teamId = await linear.TeamIdAsync(apiKey, team, ct);

        if (labels.Count > 0) context.Progress.Report(Strings.FormatCreateIssueAction_FindingLabels(string.Join(", ", labels)));
        var labelIds = await linear.LabelIdsAsync(apiKey, teamId, labels, ct);

        context.Progress.Report(Strings.CreateIssueAction_Creating);
        var issue = new NewIssue(
            teamId,
            IssueText.Title(context, context.Settings[LinearSettings.TitleTemplate] ?? LinearSettings.DefaultTitleTemplate),
            IssueText.Description(context, IncludeContext(context)),
            labelIds);
        var created = await linear.CreateIssueAsync(apiKey, issue, ct);
        return new ActionResult(Strings.FormatCreateIssueAction_Created(created.Identifier), Link: created.Url);
    }

    private string ApiKey(MessageActionContext context) =>
        context.Settings[LinearSettings.ApiKey] is { Length: > 0 } configured ? configured.Trim()
        : environment(LinearPlugin.ApiKeyVariable) is { Length: > 0 } variable ? variable.Trim()
        : throw new InvalidOperationException(
            Strings.FormatCreateIssueAction_NoApiKey(LinearPlugin.PluginId, LinearSettings.ApiKey, LinearPlugin.ApiKeyVariable));

    private static IReadOnlyList<string> Labels(MessageActionContext context) =>
        [.. (context.Settings[LinearSettings.Labels] ?? "")
            .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
            .Distinct(StringComparer.OrdinalIgnoreCase)];

    private static bool IncludeContext(MessageActionContext context) =>
        !bool.TryParse(context.Settings[LinearSettings.IncludeContext], out var include) || include;
}
