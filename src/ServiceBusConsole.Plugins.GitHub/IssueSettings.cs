namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>The plugin's settings for one run, read from <see cref="MessageActionContext.Settings"/>.</summary>
internal sealed record IssueSettings
{
    public const string RepoKey = "repo";
    public const string LabelsKey = "labels";
    public const string AssigneesKey = "assignees";
    public const string TitleTemplateKey = "titleTemplate";
    public const string CliKey = "cli";
    public const string IncludeContextKey = "includeContext";

    public const string DefaultTitleTemplate = "{entity}: {subject}";
    public const string DefaultCli = "gh";

    public required string Repo { get; init; }
    public IReadOnlyList<string> Labels { get; init; } = [];
    public IReadOnlyList<string> Assignees { get; init; } = [];
    public string TitleTemplate { get; init; } = DefaultTitleTemplate;
    public string Cli { get; init; } = DefaultCli;
    public bool IncludeContext { get; init; } = true;

    public static IssueSettings From(MessageActionContext context) => new()
    {
        Repo = Repository(context),
        Labels = List(context.Settings[LabelsKey]),
        Assignees = List(context.Settings[AssigneesKey]),
        TitleTemplate = Value(context, TitleTemplateKey) ?? DefaultTitleTemplate,
        Cli = Value(context, CliKey) ?? DefaultCli,
        IncludeContext = Value(context, IncludeContextKey) switch
        {
            null => true,
            var text when bool.TryParse(text, out var include) => include,
            var text => throw new InvalidOperationException($"plugins.{context.PluginId}.{IncludeContextKey} is '{text}': set it to true or false."),
        },
    };

    private static string Repository(MessageActionContext context)
    {
        var repo = context.RequiredSetting(RepoKey).Trim();
        return repo.Split('/') is [{ Length: > 0 }, _, ..] parts && parts.All(part => part.Length > 0)
            ? repo
            : throw new InvalidOperationException($"plugins.{context.PluginId}.{RepoKey} is '{repo}': set it to owner/name.");
    }

    private static string? Value(MessageActionContext context, string key) =>
        context.Settings[key]?.Trim() is { Length: > 0 } value ? value : null;

    private static IReadOnlyList<string> List(string? value) =>
        [.. (value ?? "").Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)];
}
