using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

public sealed class TopicListPageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static int RowOf(TerminalFrame frame, string text)
        => Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains(text));

    [Fact]
    public async Task SortingOff_RestoresLoadOrder()
    {
        var app = new AppHarness();
        app.Topics.Topics.AddRange(
        [
            new TopicInfo { Name = "zeta", SubscriptionCount = 1 },
            new TopicInfo { Name = "alpha", SubscriptionCount = 5 },
            new TopicInfo { Name = "mid", SubscriptionCount = 3 },
        ]);
        await using var terminal = await app.OpenTopicsAsync();
        await terminal.WaitForTextAsync("mid", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Subscriptions");
        var frame = await terminal.WaitForTextAsync("Subscriptions ▲", AppHarness.Timeout, Ct);
        RowOf(frame, "zeta").ShouldBeLessThan(RowOf(frame, "mid"));
        RowOf(frame, "mid").ShouldBeLessThan(RowOf(frame, "alpha"));

        await AppHarness.ClickTextAsync(terminal, "Subscriptions ▲");
        await terminal.WaitForTextAsync("Subscriptions ▼", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "Subscriptions ▼");

        frame = await terminal.WaitUntilAsync(f => !f.ContainsText("▲") && !f.ContainsText("▼"), AppHarness.Timeout, cancellationToken: Ct);
        RowOf(frame, "zeta").ShouldBeLessThan(RowOf(frame, "alpha"));
        RowOf(frame, "alpha").ShouldBeLessThan(RowOf(frame, "mid"));
    }
}
