using System.Net;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.Plugins.Linear.Tests;

public sealed class CreateIssueActionTests
{
    private const string Key = "lin_api_test";

    private readonly FakeLinearHandler _linear = new();

    private CreateIssueAction Action(string? environmentKey = null) =>
        new(new LinearClient(new HttpClient(_linear)), name => name == LinearPlugin.ApiKeyVariable ? environmentKey : null);

    private static Dictionary<string, string?> Settings(params (string Key, string? Value)[] extra) =>
        new[] { (Key: "apiKey", Value: (string?)Key), (Key: "team", Value: (string?)"ENG") }
            .Concat(extra)
            .GroupBy(s => s.Key)
            .ToDictionary(g => g.Key, g => g.Last().Value);

    [Test]
    public async Task CreatesTheIssue_AndLinksIt()
    {
        var progress = new RecordingProgress();

        var result = await Action().RunAsync(LinearTestContext.Create(Settings(), progress: progress), Ct);

        result.Text.ShouldBe("Created ENG-42");
        result.Link.ShouldBe(new Uri("https://linear.app/acme/issue/ENG-42/orders"));
        _linear.Requests.Select(r => r.Operation).ShouldBe(["Team", "Create"]);
        progress.Reports.ShouldBe(["Finding team ENG…", "Creating issue…"]);
    }

    [Test]
    public async Task SendsGraphQl_WithTheBareApiKey()
    {
        await Action().RunAsync(LinearTestContext.Create(Settings()), Ct);

        _linear.Requests.ShouldAllBe(r =>
            r.Method == HttpMethod.Post && r.Uri == LinearClient.Endpoint && r.Authorization == Key && r.ContentType == "application/json");
        _linear.Request("Team").Variables["key"]?.GetValue<string>().ShouldBe("ENG");
    }

    [Test]
    public async Task Issue_HasTitleFromTheTemplate_TeamAndMarkdownDescription()
    {
        await Action().RunAsync(LinearTestContext.Create(Settings()), Ct);

        var input = Input();
        input["teamId"]?.GetValue<string>().ShouldBe("team-1");
        input["title"]?.GetValue<string>().ShouldBe("orders DLQ: OrderPlaced");
        input.ContainsKey("labelIds").ShouldBeFalse();
        var description = input["description"]?.GetValue<string>() ?? "";
        description.ShouldStartWith("Created from [azsb](https://github.com/zidad/azure-servicebus-console) on `orders DLQ` in namespace `acme-prod`.");
        description.ShouldContain("## Message #42");
        description.ShouldContain("\"orderId\": 7");
        description.ShouldContain("## Azure Service Bus session");
        description.ShouldContain("### Where");
        description.ShouldContain("### Recent activity");
        description.ShouldContain("Opened orders DLQ");
        description.Split("## Message #42").Length.ShouldBe(2);
    }

    [Test]
    public async Task IncludeContextFalse_LeavesOutTheSession()
    {
        await Action().RunAsync(LinearTestContext.Create(Settings(("includeContext", "false"))), Ct);

        var description = Input()["description"]?.GetValue<string>() ?? "";
        description.ShouldContain("## Message #42");
        description.ShouldNotContain("Recent activity");
    }

    [Test]
    public async Task TitleTemplate_IsConfigurable()
    {
        await Action().RunAsync(LinearTestContext.Create(Settings(("titleTemplate", "[{namespace}] {deadLetterReason} #{sequenceNumber}"))), Ct);

        Input()["title"]?.GetValue<string>().ShouldBe("[acme-prod] MaxDeliveryCountExceeded #42");
    }

    [Test]
    public async Task MessageWithoutSubject_IsTitledByNumber()
    {
        await Action().RunAsync(LinearTestContext.Create(Settings(), LinearTestContext.Message with { Subject = "" }), Ct);

        Input()["title"]?.GetValue<string>().ShouldBe("orders DLQ: Message #42");
    }

    [Test]
    public async Task Labels_ResolveByName_PreferringTheTeamsOwn()
    {
        _linear.Respond("Labels", """
            {"data":{"issueLabels":{"nodes":[
              {"id":"workspace-bug","name":"bug","team":null},
              {"id":"other-bug","name":"bug","team":{"id":"team-2"}},
              {"id":"team-bug","name":"bug","team":{"id":"team-1"}},
              {"id":"servicebus","name":"servicebus","team":null}
            ]}}}
            """);
        var progress = new RecordingProgress();

        await Action().RunAsync(LinearTestContext.Create(Settings(("labels", " bug, servicebus ,")), progress: progress), Ct);

        _linear.Request("Labels").Variables["names"]?.AsArray().Select(n => n?.GetValue<string>()).ShouldBe(["bug", "servicebus"]);
        Input()["labelIds"]?.AsArray().Select(n => n?.GetValue<string>()).ShouldBe(["team-bug", "servicebus"]);
        progress.Reports.ShouldContain("Finding labels bug, servicebus…");
    }

    [Test]
    public async Task UnknownLabels_FailNamingThem_BeforeCreating()
    {
        _linear.Respond("Labels", """{"data":{"issueLabels":{"nodes":[{"id":"l1","name":"bug","team":null},{"id":"l2","name":"ops","team":{"id":"team-2"}}]}}}""");

        var error = await Should.ThrowAsync<LinearException>(
            Action().RunAsync(LinearTestContext.Create(Settings(("labels", "bug,ops,urgent"))), Ct));

        error.Message.ShouldBe("Linear has no label ops, urgent in the team or workspace");
        _linear.Requests.ShouldNotContain(r => r.Operation == "Create");
    }

    [Test]
    public async Task UnknownTeam_Fails()
    {
        _linear.Respond("Team", """{"data":{"teams":{"nodes":[]}}}""");

        var error = await Should.ThrowAsync<LinearException>(Action().RunAsync(LinearTestContext.Create(Settings(("team", "OPS"))), Ct));

        error.Message.ShouldBe("Linear has no team with key OPS");
    }

    [Test]
    public async Task GraphQlErrors_AreShown()
    {
        _linear.Respond("Create", """{"errors":[{"message":"Argument Validation Error","extensions":{"code":"INVALID_INPUT"}},{"message":"title too long"}]}""", HttpStatusCode.BadRequest);

        var error = await Should.ThrowAsync<LinearException>(Action().RunAsync(LinearTestContext.Create(Settings()), Ct));

        error.Message.ShouldBe("Linear: Argument Validation Error; title too long");
    }

    [Test]
    public async Task Unauthorized_AsksForAnotherKey()
    {
        _linear.Respond("Team", "", HttpStatusCode.Unauthorized);

        var error = await Should.ThrowAsync<LinearException>(Action().RunAsync(LinearTestContext.Create(Settings()), Ct));

        error.Message.ShouldBe("Linear rejected the API key; set plugins.linear.apiKey or LINEAR_API_KEY");
    }

    [Test]
    public async Task AuthenticationErrorInTheBody_AsksForAnotherKey()
    {
        _linear.Respond("Team", """{"errors":[{"message":"Authentication required, not authenticated","extensions":{"code":"AUTHENTICATION_ERROR"}}]}""", HttpStatusCode.BadRequest);

        var error = await Should.ThrowAsync<LinearException>(Action().RunAsync(LinearTestContext.Create(Settings()), Ct));

        error.Message.ShouldBe(LinearClient.RejectedKey);
    }

    [Test]
    public async Task ServerError_WithoutGraphQlErrors_ShowsTheStatus()
    {
        _linear.Respond("Team", "<html>bad gateway</html>", HttpStatusCode.BadGateway);

        var error = await Should.ThrowAsync<LinearException>(Action().RunAsync(LinearTestContext.Create(Settings()), Ct));

        error.Message.ShouldBe("Linear returned HTTP 502 Bad Gateway");
    }

    [Test]
    public async Task MissingApiKey_FailsBeforeCallingLinear()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(
            Action().RunAsync(LinearTestContext.Create(Settings(("apiKey", null))), Ct));

        error.Message.ShouldBe("Set a Linear API key first: azsb config set plugins.linear.apiKey <key>, or set LINEAR_API_KEY");
        _linear.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task MissingTeam_FailsBeforeCallingLinear()
    {
        var error = await Should.ThrowAsync<InvalidOperationException>(
            Action().RunAsync(LinearTestContext.Create(Settings(("team", ""))), Ct));

        error.Message.ShouldBe("Set plugins.linear.team first: azsb config set plugins.linear.team <value>");
        _linear.Requests.ShouldBeEmpty();
    }

    [Test]
    public async Task ApiKey_FallsBackToTheEnvironment()
    {
        await Action(environmentKey: "lin_api_env").RunAsync(LinearTestContext.Create(Settings(("apiKey", null))), Ct);

        _linear.Requests.ShouldAllBe(r => r.Authorization == "lin_api_env");
    }

    [Test]
    public async Task ConfiguredApiKey_WinsOverTheEnvironment()
    {
        await Action(environmentKey: "lin_api_env").RunAsync(LinearTestContext.Create(Settings()), Ct);

        _linear.Requests.ShouldAllBe(r => r.Authorization == Key);
    }

    [Test]
    public async Task Cancelled_StopsWithoutCreating()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        Exception? error = null;
        try { await Action().RunAsync(LinearTestContext.Create(Settings()), cancelled.Token); }
        catch (Exception e) { error = e; }

        error.ShouldBeAssignableTo<OperationCanceledException>();
        _linear.Requests.ShouldBeEmpty();
    }

    private JsonObject Input() => _linear.Request("Create").Variables["input"]?.AsObject() ?? throw new InvalidOperationException("No input");
}
