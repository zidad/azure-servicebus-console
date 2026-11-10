using ServiceBusConsole.Plugins.GitHub.Resources;

namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>Creates an issue for the message with <c>gh issue create</c>, signed in as the user's GitHub CLI login.</summary>
internal sealed class CreateIssueAction : IMessageAction
{
    public string PluginId => GitHubPlugin.PluginId;

    public string Id => "create-issue";

    public string Label => Strings.CreateIssueAction_Label;

    public string Icon => "🐙";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var settings = IssueSettings.From(context);
        var bodyFile = Path.Combine(PluginFiles.PluginDirectory(context), PluginFiles.MessageFileName(context, "md"));
        await File.WriteAllTextAsync(bodyFile, IssueBody.Format(context, settings.IncludeContext), ct);

        context.Progress.Report(Strings.FormatCreateIssueAction_Creating(settings.Repo));
        var spec = Spec(settings, IssueTitle.Format(settings.TitleTemplate, context), bodyFile);
        var command = $"{settings.Cli} issue create";
        var result = (await RunCliAsync(context, spec, ct)).EnsureSuccess(command);
        var url = IssueUrl(result.StandardOutput)
            ?? throw new InvalidOperationException(Strings.FormatCreateIssueAction_NoIssueUrl(command, result.StandardOutput.Trim()));
        return new ActionResult(Strings.FormatCreateIssueAction_Created(url.Segments[^1]), Link: url);
    }

    public static ProcessSpec Spec(IssueSettings settings, string title, string bodyFile) => new(settings.Cli,
    [
        "issue", "create",
        "--repo", settings.Repo,
        "--title", title,
        "--body-file", bodyFile,
        .. settings.Labels.SelectMany(label => (string[])["--label", label]),
        .. settings.Assignees.SelectMany(assignee => (string[])["--assignee", assignee]),
    ]);

    /// <summary>The last line <c>gh issue create</c> prints is the new issue's URL.</summary>
    public static Uri? IssueUrl(string standardOutput) =>
        standardOutput.Split('\n', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries).LastOrDefault() is { } line
        && Uri.TryCreate(line, UriKind.Absolute, out var url)
        && (url.Scheme == Uri.UriSchemeHttps || url.Scheme == Uri.UriSchemeHttp)
            ? url
            : null;

    private static async Task<ProcessResult> RunCliAsync(MessageActionContext context, ProcessSpec spec, CancellationToken ct)
    {
        try
        {
            return await context.Host.RunProcessAsync(spec, ct);
        }
        catch (InvalidOperationException e)
        {
            throw new InvalidOperationException(
                Strings.FormatCreateIssueAction_CouldNotRunCli(spec.FileName, context.PluginId, IssueSettings.CliKey), e);
        }
    }
}
