namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>Creates an issue for the message with <c>gh issue create</c>, signed in as the user's GitHub CLI login.</summary>
internal sealed class CreateIssueAction : IMessageAction
{
    public string PluginId => GitHubPlugin.PluginId;

    public string Id => "create-issue";

    public string Label => "Create GitHub issue";

    public string Icon => "🐙";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var settings = IssueSettings.From(context);
        var bodyFile = Path.Combine(PluginFiles.PluginDirectory(context), PluginFiles.MessageFileName(context, "md"));
        await File.WriteAllTextAsync(bodyFile, IssueBody.Format(context, settings.IncludeContext), ct);

        context.Progress.Report($"Creating an issue in {settings.Repo}…");
        var spec = Spec(settings, IssueTitle.Format(settings.TitleTemplate, context), bodyFile);
        var command = $"{settings.Cli} issue create";
        var result = (await RunCliAsync(context, spec, ct)).EnsureSuccess(command);
        var url = IssueUrl(result.StandardOutput)
            ?? throw new InvalidOperationException($"{command} printed no issue URL: {result.StandardOutput.Trim()}");
        return new ActionResult($"Created issue #{url.Segments[^1]}", Link: url);
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
                $"Could not run the GitHub CLI ({spec.FileName}): install it from https://cli.github.com and sign in with gh auth login, " +
                $"or point to it: azsb config set plugins.{context.PluginId}.{IssueSettings.CliKey} <path>", e);
        }
    }
}
