namespace ServiceBusConsole.Plugins.GitHub.Tests;

public sealed class IssueTitleTests
{
    private static readonly FakePluginHost Host = new();

    private static string Title(MessageInfo message, string template = IssueSettings.DefaultTitleTemplate) =>
        IssueTitle.Format(template, Contexts.Create(Host, message));

    [Test]
    public void DefaultTemplate_IsTheEntityAndSubject() =>
        Title(Contexts.Message).ShouldBe("orders DLQ: order-created");

    [Test]
    public void WithoutSubject_TheMessageIdStandsIn_ElseTheSequenceNumber()
    {
        Title(Contexts.Message with { Subject = "" }).ShouldBe("orders DLQ: msg-42");
        Title(Contexts.Message with { Subject = "", MessageId = "" }).ShouldBe("orders DLQ: #42");
        Title(Contexts.Message with { Subject = "" }, "{SUBJECT} ({deadLetterReason})").ShouldBe("msg-42 (MaxDeliveryCountExceeded)");
    }

    [Test]
    public void EmptyTitle_FallsBackToTheSequenceNumber() =>
        Title(Contexts.Message with { DeadLetterReason = null }, " {deadLetterReason} ").ShouldBe("Message #42");

    [Test]
    public void LineBreaksAndLength_AreKeptWithinGitHubsLimits()
    {
        Title(Contexts.Message with { Subject = "a\nb" }).ShouldBe("orders DLQ: a b");
        var longTitle = Title(Contexts.Message with { Subject = new string('x', 300) });
        longTitle.Length.ShouldBe(IssueTitle.MaxLength);
        longTitle.ShouldEndWith("…");
    }
}
