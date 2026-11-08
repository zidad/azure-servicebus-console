namespace ServiceBusConsole.Plugins.GitHub.Tests;

public sealed class CreateIssueActionTests
{
    private static readonly (string, string?) Repo = ("repo", "zidad/example");

    private static Task<ActionResult> Run(FakePluginHost host, params (string Key, string? Value)[] settings) =>
        new CreateIssueAction().RunAsync(Contexts.Create(host, settings: settings), Ct);

    [Test]
    public async Task RunsGhIssueCreate_WithTheRepoTitleAndBodyFile()
    {
        var host = new FakePluginHost();

        await Run(host, Repo);

        var spec = host.Processes.ShouldHaveSingleItem();
        spec.FileName.ShouldBe("gh");
        var bodyFile = Path.Combine(host.StateDirectory, "plugins", "github", "orders-dlq-42.md");
        spec.Arguments.ShouldBe(["issue", "create", "--repo", "zidad/example", "--title", "orders DLQ: order-created", "--body-file", bodyFile]);
        host.BodyFiles.ShouldHaveSingleItem().ShouldContain("## Message #42");
    }

    [Test]
    public async Task LabelsAndAssignees_AreEachTheirOwnArgument()
    {
        var host = new FakePluginHost();

        await Run(host, Repo, ("labels", "bug, dead-letter ,,"), ("assignees", "@me,octocat"), ("cli", "/opt/gh"));

        var spec = host.Processes.ShouldHaveSingleItem();
        spec.FileName.ShouldBe("/opt/gh");
        spec.Arguments.Skip(8).ShouldBe(["--label", "bug", "--label", "dead-letter", "--assignee", "@me", "--assignee", "octocat"]);
    }

    [Test]
    public async Task TheIssueUrl_IsTheResultsLink()
    {
        var host = new FakePluginHost { Result = new(0, "\nCreating issue in zidad/example\n\nhttps://github.com/zidad/example/issues/12\n", "") };

        var result = await Run(host, Repo);

        result.Link.ShouldBe(new Uri("https://github.com/zidad/example/issues/12"));
        result.Text.ShouldBe("Created issue #12");
    }

    [Test]
    public async Task NoUrlPrinted_Fails()
    {
        var host = new FakePluginHost { Result = new(0, "done\n", "") };

        (await Should.ThrowAsync<InvalidOperationException>(() => Run(host, Repo))).Message.ShouldContain("printed no issue URL");
    }

    [Test]
    public async Task MissingRepo_TellsHowToSetIt_WithoutRunningGh()
    {
        var host = new FakePluginHost();

        (await Should.ThrowAsync<InvalidOperationException>(() => Run(host))).Message
            .ShouldBe("Set plugins.github.repo first: azsb config set plugins.github.repo <value>");
        (await Should.ThrowAsync<InvalidOperationException>(() => Run(host, ("repo", "example")))).Message.ShouldContain("owner/name");
        host.Processes.ShouldBeEmpty();
    }

    [Test]
    public async Task InvalidIncludeContext_IsRejected()
    {
        (await Should.ThrowAsync<InvalidOperationException>(() => Run(new FakePluginHost(), Repo, ("includeContext", "maybe")))).Message
            .ShouldContain("true or false");
    }

    [Test]
    public async Task GhMissing_SaysToInstallItOrSetTheCli()
    {
        var host = new FakePluginHost { StartFailure = new InvalidOperationException("Could not start gh: No such file or directory") };

        var error = await Should.ThrowAsync<InvalidOperationException>(() => Run(host, Repo));

        error.Message.ShouldContain("https://cli.github.com");
        error.Message.ShouldContain("azsb config set plugins.github.cli <path>");
    }

    [Test]
    public async Task GhFailing_ReportsItsError()
    {
        var host = new FakePluginHost { Result = new(4, "", "To get started with GitHub CLI, please run:  gh auth login\n") };

        var error = await Should.ThrowAsync<ProcessFailedException>(() => Run(host, Repo));

        error.Message.ShouldBe("gh issue create failed (exit code 4): To get started with GitHub CLI, please run:  gh auth login");
    }

    [Test]
    public void HasTheCreateIssueAction()
    {
        var action = new CreateIssueAction();
        action.PluginId.ShouldBe(new GitHubPlugin().Id);
        action.Icon.ShouldBe("🐙");
    }

    [Test]
    public void IsOnByDefault_WhenTheGitHubCliIsFound()
    {
        var plugin = new GitHubPlugin();
        var environment = new PluginEnvironment(command => command == "gh", _ => null);

        plugin.Missing(Contexts.Create(new FakePluginHost()).Settings, environment).ShouldBeNull();
        plugin.Missing(Contexts.Create(new FakePluginHost(), settings: ("cli", "/opt/gh")).Settings, environment).ShouldBe("/opt/gh not found");
    }
}
