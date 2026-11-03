using System.Text;

namespace ServiceBusConsole.Plugins.GitHub.Tests;

public sealed class IssueBodyTests
{
    private static readonly FakePluginHost Host = new();

    private static string Body(MessageInfo message, bool includeContext = true) =>
        IssueBody.Format(Contexts.Create(Host, message), includeContext);

    [Test]
    public void SaysWhereItCameFrom_ThenTheMessage_ThenTheSessionContext()
    {
        var body = Body(Contexts.Message);

        body.ShouldStartWith("Created with [azsb](https://github.com/zidad/azure-servicebus-console) from message #42 in `orders DLQ`.\n\n## Message #42\n");
        body.ShouldContain("- Dead-letter reason: MaxDeliveryCountExceeded");
        body.ShouldContain("| tenant | contoso |");
        body.ShouldContain("\"orderId\": 7");
        body.ShouldContain("\n## Azure Service Bus session\n");
        body.ShouldContain("\n### Where\n");
        body.ShouldContain("- Namespace: sb-orders-dev");
        body.ShouldContain("\n### Recent activity\n");
        body.ShouldContain("Loaded (orders DLQ): Peeked 10 messages");
        body.IndexOf("## Message #42", StringComparison.Ordinal).ShouldBe(body.LastIndexOf("## Message #42", StringComparison.Ordinal));
    }

    [Test]
    public void WithoutContext_IsJustTheMessage()
    {
        var body = Body(Contexts.Message, includeContext: false);

        body.ShouldContain("## Message #42");
        body.ShouldNotContain("Recent activity");
        body.ShouldNotContain("sb-orders-dev");
    }

    [Test]
    public void LongBody_IsTruncatedToGitHubsLimit_WithANote_KeepingTheContext()
    {
        var text = new string('x', 100_000);

        var body = Body(Contexts.Message with { Body = Encoding.UTF8.GetBytes(text) });

        body.Length.ShouldBeLessThanOrEqualTo(IssueBody.MaxLength);
        body.Length.ShouldBeGreaterThan(IssueBody.MaxLength - 1_000);
        body.ShouldContain("### Recent activity");
        body.ShouldContain("Truncated to fit GitHub's limit of 65,536 characters; the message body has 100,000.");
        body.Split("```").Length.ShouldBe(3);
    }

    [Test]
    public void TooLongWithoutRoomInTheBody_IsCutWhole()
    {
        var message = Contexts.Message with { Properties = [new("huge", new string('p', 70_000))] };

        var body = Body(message);

        body.Length.ShouldBeLessThanOrEqualTo(IssueBody.MaxLength);
        body.ShouldContain("Truncated to fit GitHub's limit");
    }
}
