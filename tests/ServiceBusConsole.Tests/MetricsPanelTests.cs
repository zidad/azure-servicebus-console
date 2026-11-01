using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(MetricsPanelTests))]
public sealed class MetricsPanelTests
{
    private static int Rows(TerminalFrame frame, string prefix)
        => Enumerable.Range(0, frame.Height).Count(y => frame.GetLine(y).StartsWith($"│ {prefix}", StringComparison.Ordinal));

    private static bool ChartsShown(TerminalFrame frame, string label)
        => frame.ContainsText(label) && frame.ContainsText("-24h") && frame.ContainsText("now") && !frame.ContainsText("Loading metrics");

    [Test]
    public async Task QueueList_ChartsTheSelectedQueue()
    {
        var app = new AppHarness();
        app.Queues.Queues.AddRange(
        [
            new QueueInfo { Name = "payments", DeadLetterMessageCount = 3 },
            new QueueInfo { Name = "orders", ActiveMessageCount = 12 },
        ]);
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);

        var frame = await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue payments"), AppHarness.Timeout, cancellationToken: Ct);

        frame.ContainsText("incoming").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("dead-lettered").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("last 24 h").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("[F12] 📈 Metrics").ShouldBeTrue(frame.ScreenText);
        app.Metrics.Requested.ShouldContain(new MetricsScope.Entity("payments"));
    }

    [Test]
    public async Task F5_ReloadsTheCharts()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);

        await terminal.SendKeyAsync(ConsoleKey.F5, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(_ => app.Metrics.Requested.Count(s => s == new MetricsScope.Entity("orders")) == 2, AppHarness.Timeout, cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task MovingTheSelection_LoadsOnlyTheRowItSettlesOn()
    {
        var app = new AppHarness();
        app.MetricsOptions.Debounce = TimeSpan.FromSeconds(1);
        app.Queues.Queues.AddRange(
        [
            new QueueInfo { Name = "alpha", DeadLetterMessageCount = 4 },
            new QueueInfo { Name = "bravo", DeadLetterMessageCount = 3 },
            new QueueInfo { Name = "charlie", DeadLetterMessageCount = 2 },
            new QueueInfo { Name = "delta", DeadLetterMessageCount = 1 },
        ]);
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue alpha"), AppHarness.Timeout, cancellationToken: Ct);

        for (var i = 0; i < 3; i++) await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("📬 queue delta", AppHarness.Timeout, Ct);
        frame.ContainsText("Loading metrics").ShouldBeTrue(frame.ScreenText);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue delta"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.Requested.OfType<MetricsScope.Entity>().Select(e => e.Name).ShouldBe(["alpha", "delta"]);
    }

    [Test]
    public async Task AllSubscriptions_ChartTheTopicOfTheSelectedSubscription()
    {
        var app = new AppHarness();
        app.Topics.Subscriptions.AddRange(
        [
            new SubscriptionInfo { TopicName = "order-events", Name = "shipping", DeadLetterMessageCount = 3 },
            new SubscriptionInfo { TopicName = "notifications", Name = "push", DeadLetterMessageCount = 2 },
            new SubscriptionInfo { TopicName = "order-events", Name = "billing", DeadLetterMessageCount = 1 },
        ]);
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F3);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📢 topic order-events"), AppHarness.Timeout, cancellationToken: Ct);

        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => ChartsShown(f, "📢 topic notifications"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.Requested.OfType<MetricsScope.Entity>().Select(e => e.Name).ShouldBe(["order-events", "notifications"]);
    }

    [Test]
    public async Task SubscriptionsOfATopic_ChartTheTopic()
    {
        var app = new AppHarness();
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events", SubscriptionCount = 1 });
        app.Topics.Subscriptions.Add(new SubscriptionInfo { TopicName = "order-events", Name = "shipping" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F2);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📢 topic order-events"), AppHarness.Timeout, cancellationToken: Ct);
        var topicTable = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await AppHarness.WaitForTableFocusAsync(terminal, topicTable);
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("shipping") && ChartsShown(f, "📢 topic order-events"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("📥 order-events").ShouldBeTrue(frame.ScreenText);
        app.Metrics.Requested.OfType<MetricsScope.Entity>().Select(e => e.Name).ShouldAllBe(name => name == "order-events");
    }

    [Test]
    public async Task F12_HidesThePanel_GivingTheTableItsLines_AndShowsItAgain()
    {
        var app = new AppHarness();
        app.Queues.Queues.AddRange(Enumerable.Range(0, 40).Select(i => new QueueInfo { Name = $"q{i:00}" }));
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        var frame = await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue q00"), AppHarness.Timeout, cancellationToken: Ct);
        var rowsWithPanel = Rows(frame, "q");

        await terminal.SendKeyAsync(ConsoleKey.F12, '\0', cancellationToken: Ct);

        frame = await terminal.WaitUntilAsync(f => !f.ContainsText("last 24 h") && Rows(f, "q") > rowsWithPanel, AppHarness.Timeout, cancellationToken: Ct);
        (Rows(frame, "q") - rowsWithPanel).ShouldBe(FullscreenLayout.MetricsPanel);
        frame.ContainsText("[F12] 📈 Metrics").ShouldBeTrue(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.F12, '\0', cancellationToken: Ct);

        frame = await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue q00"), AppHarness.Timeout, cancellationToken: Ct);
        Rows(frame, "q").ShouldBe(rowsWithPanel);
    }

    [Test]
    public async Task ShortTerminal_ShowsNoPanel()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1, height: 34);

        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await Task.Delay(app.MetricsOptions.Debounce * 2, Ct);

        var frame = terminal.CurrentFrame;
        frame.ContainsText("last 24 h").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("[F12] 📈 Metrics").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task Emulator_HasNoPanelAndNoMetricsButton()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync(height: 40);

        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await Task.Delay(app.MetricsOptions.Debounce * 2, Ct);

        var frame = terminal.CurrentFrame;
        frame.ContainsText("last 24 h").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("[F12]").ShouldBeFalse(frame.ScreenText);
        app.Metrics.Requested.ShouldBeEmpty();
    }

    [Test]
    public async Task FailedLoad_ShowsTheFirstLineOfTheError_InThePanel()
    {
        var app = new AppHarness();
        app.Metrics.LoadError = new InvalidOperationException("Forbidden by policy\nStatus: 403 (Forbidden)");
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);

        var frame = await terminal.WaitForTextAsync("Metrics unavailable: Forbidden by policy", AppHarness.Timeout, Ct);

        frame.ContainsText("Status: 403").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("Dismiss").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("📬 queue orders").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task ConnectionPage_ChartsAnAzureNamespace_InPlaceOfTheLogo()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitForTextAsync(AppHarness.AzureSubscription.Name, AppHarness.Timeout, Ct);
        terminal.CurrentFrame.ContainsText("Work In Progress").ShouldBeTrue(terminal.CurrentFrame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);

        var frame = await terminal.WaitUntilAsync(f => ChartsShown(f, "🌐 namespace sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Work In Progress").ShouldBeFalse(frame.ScreenText);
        app.Metrics.Requested.ShouldBe([new MetricsScope.Namespace()]);

        await terminal.SendKeyAsync(ConsoleKey.UpArrow, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.ContainsText("Work In Progress") && !f.ContainsText("last 24 h"), AppHarness.Timeout, cancellationToken: Ct);
    }
}
