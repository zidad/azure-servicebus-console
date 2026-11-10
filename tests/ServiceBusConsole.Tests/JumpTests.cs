namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(JumpTests))]
public sealed class JumpTests
{
    private static readonly JumpTarget[] Targets =
    [
        new JumpTarget.Queue(new QueueInfo { Name = "orders" }),
        new JumpTarget.Queue(new QueueInfo { Name = "payments", DeadLetterMessageCount = 3 }),
        new JumpTarget.Queue(new QueueInfo { Name = "payments-retry" }),
        new JumpTarget.Topic(new TopicInfo { Name = "order-events" }),
        new JumpTarget.Subscription(new SubscriptionInfo { TopicName = "order-events", Name = "payments" }),
    ];

    private static string[] Names(string query) => [.. JumpIndex.Search(query, Targets).Select(t => t.Name)];

    [Test]
    public void Search_PrefixThenContainsThenLettersInOrder_DeadLettersFirstWithinEach()
    {
        Names("pay").ShouldBe(["payments", "payments-retry", "order-events/payments"]);
        Names("order").ShouldBe(["order-events", "order-events/payments", "orders"]);
        Names("pmtr").ShouldBe(["payments-retry"]);
        Names("zzz").ShouldBeEmpty();
    }

    [Test]
    public void Search_EmptyQuery_ListsDeadLettersFirst()
    {
        Names("")[0].ShouldBe("payments");
        JumpIndex.Search("", Targets, max: 2).Count.ShouldBe(2);
    }

    [Test]
    public void Grouped_KeepsRankWithinEachKind_InKindOrder()
    {
        var groups = JumpIndex.Grouped("pay", Targets);

        groups.Select(g => g.Kind).ShouldBe([JumpKind.Queue, JumpKind.Subscription]);
        groups[0].Targets.Select(t => t.Name).ShouldBe(["payments", "payments-retry"]);
        groups[1].Targets.Select(t => t.Name).ShouldBe(["order-events/payments"]);
        JumpIndex.Grouped("order", Targets).Select(g => g.Kind).ShouldBe([JumpKind.Queue, JumpKind.Topic, JumpKind.Subscription]);
    }

    [Test]
    public async Task Colon_FindsASubscription_AndOpensItsBrowser()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events", SubscriptionCount = 1 });
        app.Topics.Subscriptions.Add(new SubscriptionInfo { TopicName = "order-events", Name = "billing", DeadLetterMessageCount = 2 });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        var table = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await terminal.WaitForTextAsync("🔍 Jump to", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        await terminal.TypeAsync("bill", Ct);
        var frame = await terminal.WaitForTextAsync("📥 order-events › billing", AppHarness.Timeout, Ct);
        frame.ContainsText("💀 2").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("📬 orders").ShouldBeFalse(frame.ScreenText);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("📥 billing"), AppHarness.Timeout, cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => app.Messages.Peeked.Count > 0, AppHarness.Timeout, cancellationToken: Ct);
        app.Messages.Peeked.ShouldBe([new MessageSource.Subscription("order-events", "billing", IsDlq: false)]);
    }

    [Test]
    public async Task Tab_OpensTheMatchsDeadLetterQueue_UnderItsGroup()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Queues.Queues.Add(new QueueInfo { Name = "payments", DeadLetterMessageCount = 3 });
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events", SubscriptionCount = 1 });
        app.Topics.Subscriptions.Add(new SubscriptionInfo { TopicName = "order-events", Name = "billing" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (2)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        var table = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await terminal.WaitForTextAsync("🔍 Jump to", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        var input = terminal.FocusKey;
        await terminal.TypeAsync("zz", Ct);
        await terminal.WaitForTextAsync("Nothing matches.", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);
        await AppHarness.WaitForSettledAsync(terminal, _ => terminal.FocusKey == input);
        await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.TypeAsync("pay", Ct);
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("📬 payments") && !f.ContainsText("📬 orders"), AppHarness.Timeout, cancellationToken: Ct);
        var header = frame.ScreenText.Split('\n').ToList();
        header.FindIndex(l => l.Contains(" QUEUES")).ShouldBe(header.FindIndex(l => l.Contains("📬 payments")) - 1, frame.ScreenText);
        frame.ContainsText(" TOPICS").ShouldBeFalse(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("payments") && !f.ContainsText("Jump to"), AppHarness.Timeout, cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => app.Messages.Peeked.Count > 0, AppHarness.Timeout, cancellationToken: Ct);
        app.Messages.Peeked.ShouldBe([new MessageSource.Queue("payments", IsDlq: true)]);
    }
}
