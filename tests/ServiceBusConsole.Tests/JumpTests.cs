using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using RazorConsole.Testing;

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

    private static readonly JumpNamespace Production = new(new AzureSubscription("Contoso Production", "prod-id"), new NamespaceInfo
    {
        Name = "sb-payments-prd", FullyQualifiedNamespace = "sb-payments-prd.servicebus.windows.net", ResourceGroup = "rg-prd", Location = "West Europe", Sku = "Standard",
    });

    [Test]
    public void Search_PutsTheCurrentNamespaceFirst_AndNamespacesInTheirOwnGroup()
    {
        JumpTarget[] targets =
        [
            new JumpTarget.Queue(new QueueInfo { Name = "payments" }) { Elsewhere = Production },
            new JumpTarget.Queue(new QueueInfo { Name = "payments" }),
            new JumpTarget.Namespace(Production),
        ];

        JumpIndex.Search("pay", targets).Select(t => t.OtherNamespace).ShouldBe([null, Production, Production]);
        var groups = JumpIndex.Grouped("pay", targets);
        groups.Select(g => g.Kind).ShouldBe([JumpKind.Queue, JumpKind.Namespace]);
        groups[1].Targets.Single().Name.ShouldBe("sb-payments-prd");
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

    private static async Task<(AppHarness App, HeadlessTerminal Terminal)> JumpToPaymentsAsync()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Queues.Queues.Add(new QueueInfo { Name = "payments", DeadLetterMessageCount = 2 });
        app.Messages.Messages.AddRange([
            new MessageInfo { SequenceNumber = 1, State = MessageState.Peeked, Subject = "first", EnqueuedTime = DateTimeOffset.UnixEpoch },
            new MessageInfo { SequenceNumber = 2, State = MessageState.Peeked, Subject = "second", EnqueuedTime = DateTimeOffset.UnixEpoch }]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (2)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        var table = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        await terminal.TypeAsync("pay", Ct);
        await terminal.WaitForTextAsync("🔁 Retry dead-letter of payments", AppHarness.Timeout, Ct);
        return (app, terminal);
    }

    [Test]
    public async Task Actions_ShowForTheMatchWithDeadLetters_AndRunFromTheCursor()
    {
        var (app, terminal) = await JumpToPaymentsAsync();
        await using var _ = terminal;
        var frame = terminal.CurrentFrame;
        frame.ContainsText(" ACTIONS").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("💀 Open dead-letter of payments").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("🧹 Purge dead-letter of payments").ShouldBeTrue(frame.ScreenText);

        // Down from the only match onto the actions: Purge is the second.
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Type yes to confirm", AppHarness.Timeout, Ct);
        app.Messages.Peeked.ShouldBe([new MessageSource.Queue("payments", IsDlq: true)]);
        await AppHarness.WaitForFocusAsync(terminal, "0");
        await terminal.TypeAsync("yes\r", Ct);
        await terminal.WaitForTextAsync("Purged 2 message(s).", AppHarness.Timeout, Ct);
        app.Messages.Purged.ShouldBe([new MessageSource.Queue("payments", IsDlq: true)]);
    }

    [Test]
    public async Task CtrlR_RetriesTheMatchsDeadLetters_InItsBrowser()
    {
        var (app, terminal) = await JumpToPaymentsAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.R, '\u0012', ConsoleModifiers.Control, Ct);

        var confirm = await terminal.WaitForTextAsync("PeekLock → Requeue to payments → Mark complete", AppHarness.Timeout, Ct);
        confirm.ContainsText("Retry 2 messages?").ShouldBeTrue(confirm.ScreenText);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retried 2 of 2.", AppHarness.Timeout, Ct);
        app.Messages.Sent.Select(s => s.SequenceNumber).ShouldBe([1L, 2L]);
        terminal.Services.GetRequiredService<ActivityLog>().Entries
            .ShouldContain(e => e.Kind == ActivityKind.Operation && e.Text.Contains("Retried 2 of 2."));
    }

    [Test]
    public async Task Actions_NotOfferedWithoutDeadLetters()
    {
        var (_, terminal) = await JumpToPaymentsAsync();
        await using var __ = terminal;
        for (var i = 0; i < 3; i++) await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.TypeAsync("orders", Ct);
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("📬 orders") && !f.ContainsText("📬 payments"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText(" ACTIONS").ShouldBeFalse(frame.ScreenText);
    }

    // Development is open; Production's namespace and its queues are only in the cache.
    private static async Task<(AppHarness App, HeadlessTerminal Terminal)> JumpFromDevelopmentAsync()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(Production.Subscription);
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        var cache = new FileCache(NullLogger<FileCache>.Instance, app.CacheDirectory);
        await cache.SaveAsync(CacheKeys.Namespaces("prod-id"), [Production.Info]);
        await cache.SaveAsync(CacheKeys.Queues(Production.Info), [new QueueInfo { Name = "refunds", DeadLetterMessageCount = 4 }]);
        var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var table = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Oem1, ':', ConsoleModifiers.Shift, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        await terminal.WaitForTextAsync("in sb-orders-dev · ^a all namespaces", AppHarness.Timeout, Ct);
        return (app, terminal);
    }

    [Test]
    public async Task ANamespaceOfAnotherSubscription_SwitchesTheConnection()
    {
        var (app, terminal) = await JumpFromDevelopmentAsync();
        await using var _ = terminal;

        await terminal.TypeAsync("prd", Ct);
        var frame = await terminal.WaitForTextAsync("🌐 sb-payments-prd", AppHarness.Timeout, Ct);
        frame.ContainsText(" OTHER NAMESPACES").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Contoso Production").ShouldBeTrue(frame.ScreenText);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("sb-payments-prd") && f.GetLine(0).Contains("Contoso Production"), AppHarness.Timeout, cancellationToken: Ct);
        app.Connection.CurrentNamespace.ShouldBe(Production.Info.FullyQualifiedNamespace);
        app.NavState.AzureSubscription.ShouldBe(Production.Subscription);
    }

    [Test]
    public async Task CtrlA_SearchesTheCachedEntitiesOfAllNamespaces()
    {
        var (app, terminal) = await JumpFromDevelopmentAsync();
        await using var _ = terminal;

        await terminal.TypeAsync("refu", Ct);
        await terminal.WaitForTextAsync("Nothing matches.", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.A, '\u0001', ConsoleModifiers.Control, Ct);
        var frame = await terminal.WaitForTextAsync("📬 refunds", AppHarness.Timeout, Ct);
        frame.ContainsText("in all namespaces · ^a sb-orders-dev only").ShouldBeTrue(frame.ScreenText);
        Line(frame, "📬 refunds").ShouldContain("sb-payments-prd");
        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("sb-payments-prd") && f.GetLine(0).Contains("refunds"), AppHarness.Timeout, cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => app.Messages.Peeked.Count > 0, AppHarness.Timeout, cancellationToken: Ct);
        app.Messages.Peeked.ShouldBe([new MessageSource.Queue("refunds", IsDlq: true)]);
        app.Connection.CurrentNamespace.ShouldBe(Production.Info.FullyQualifiedNamespace);
    }

    private static string Line(TerminalFrame frame, string text)
        => frame.GetLine(Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains(text)));
}
