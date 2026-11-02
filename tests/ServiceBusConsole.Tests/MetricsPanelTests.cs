using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(MetricsPanelTests))]
public sealed class MetricsPanelTests
{
    private static int Rows(TerminalFrame frame, string prefix)
        => Enumerable.Range(0, frame.Height).Count(y => frame.GetLine(y).StartsWith($"│ {prefix}", StringComparison.Ordinal));

    private static bool ChartsShown(TerminalFrame frame, string label, string from = "-24h")
        => frame.ContainsText(label) && frame.ContainsText(from) && frame.ContainsText("now") && !frame.ContainsText("Loading metrics");

    private static int LineOf(TerminalFrame frame, string text)
        => Enumerable.Range(0, frame.Height).FirstOrDefault(y => frame.GetLine(y).Contains(text, StringComparison.Ordinal), -1);

    private static Task Key(HeadlessTerminal terminal, char key)
        => terminal.SendKeyAsync(key is '+' or '=' ? ConsoleKey.OemPlus : ConsoleKey.OemMinus, key, cancellationToken: Ct);

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
    public async Task ZoomKeys_StepTheRange_AndLoadItsWindow()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.Windows.Last().Interval.ShouldBe(TimeSpan.FromMinutes(30));

        await Key(terminal, '_');

        await terminal.WaitUntilAsync(f => f.ContainsText("last 3 d") && ChartsShown(f, "📬 queue orders", "-3d"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.Windows.Last().ShouldSatisfyAllConditions(
            w => (w.To - w.From).ShouldBe(TimeSpan.FromDays(3)),
            w => w.Interval.ShouldBe(TimeSpan.FromHours(1)));

        for (var i = 0; i < 3; i++) await Key(terminal, '+');
        for (var i = 0; i < 3; i++) await Key(terminal, '=');

        await terminal.WaitUntilAsync(f => f.ContainsText("last 1 h") && ChartsShown(f, "📬 queue orders", "-60m"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.Windows.Last().ShouldSatisfyAllConditions(
            w => (w.To - w.From).ShouldBe(TimeSpan.FromHours(1)),
            w => w.Interval.ShouldBe(TimeSpan.FromMinutes(1)));
        app.Metrics.Requested.ShouldAllBe(s => s == new MetricsScope.Entity("orders"));
    }

    [Test]
    public async Task AtTheNarrowestRange_ZoomingIn_LoadsNothing()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);
        for (var i = 0; i < 4; i++) await Key(terminal, '+');
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders", "-60m"), AppHarness.Timeout, cancellationToken: Ct);
        var loads = app.Metrics.Windows.Count;

        await Key(terminal, '=');
        await Task.Delay(app.MetricsOptions.RangeDebounce * 3, Ct);

        app.Metrics.Windows.Count.ShouldBe(loads);
        terminal.CurrentFrame.ContainsText("last 1 h").ShouldBeTrue(terminal.CurrentFrame.ScreenText);
    }

    [Test]
    public async Task Minus_GoesIntoAFilterThatHoldsText_Plus_StillZoomsIn()
    {
        var app = new AppHarness();
        app.Queues.Queues.AddRange([new QueueInfo { Name = "order-events" }, new QueueInfo { Name = "orders" }]);
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue order-events"), AppHarness.Timeout, cancellationToken: Ct);

        await terminal.TypeAsync("order", Ct);
        await Key(terminal, '-');
        await Key(terminal, '+');

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("🔍 order-") && f.ContainsText("last 12 h"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("🔍 order-+").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("(1 of 2)").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task RangeButtons_StepOnSpaceOrEnterAfterTab_AndOnClick()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);

        var table = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);
        await AppHarness.WaitForFocusAsync(terminal, "2", table);
        await terminal.SendKeyAsync(ConsoleKey.Spacebar, ' ', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => f.ContainsText("last 3 d") && ChartsShown(f, "📬 queue orders", "-3d"), AppHarness.Timeout, cancellationToken: Ct);

        var minus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);
        await AppHarness.WaitForFocusAsync(terminal, "2", minus);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);

        await AppHarness.ClickTextAsync(terminal, "[+]");
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders", "-12h"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.ClickTextAsync(terminal, "[-]");
        await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue orders"), AppHarness.Timeout, cancellationToken: Ct);
        terminal.CurrentFrame.ContainsText("🔍 type to filter").ShouldBeTrue(terminal.CurrentFrame.ScreenText);
    }

    [Test]
    public async Task LoadingChartsAndErrors_KeepTheTableAndButtonsInPlace()
    {
        var app = new AppHarness();
        app.MetricsOptions.Debounce = TimeSpan.FromSeconds(1);
        app.Queues.Queues.AddRange(Enumerable.Range(0, 40).Select(i => new QueueInfo { Name = $"q{i:00}" }));
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1);

        var loading = await terminal.WaitUntilAsync(f => f.ContainsText("Loading metrics"), AppHarness.Timeout, cancellationToken: Ct);
        var charts = await terminal.WaitUntilAsync(f => ChartsShown(f, "📬 queue q00"), AppHarness.Timeout, cancellationToken: Ct);
        app.Metrics.LoadError = new InvalidOperationException("Forbidden");
        await terminal.SendKeyAsync(ConsoleKey.F5, '\0', cancellationToken: Ct);
        var error = await terminal.WaitForTextAsync("Metrics unavailable: Forbidden", AppHarness.Timeout, Ct);

        TerminalFrame[] frames = [loading, charts, error];
        frames.Select(f => (Rows(f, "q"), LineOf(f, "q00"), LineOf(f, "📬 queue q00"), LineOf(f, "[F5] 🔄 Refresh")))
            .Distinct().ShouldHaveSingleItem();
    }

    [Test]
    public async Task ConnectionPage_ChartsTheNamespaceBelowTheLists_WhichNeverMove()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
        await using var terminal = await app.StartAsync(height: 50);
        await AppHarness.WaitForTableFocusAsync(terminal);
        var emulator = await terminal.WaitUntilAsync(
            f => f.ContainsText(AppHarness.AzureSubscription.Name) && !f.ContainsText("Listing Azure subscriptions")
                && f.ContainsText("aren't available for localhost"),
            AppHarness.Timeout, cancellationToken: Ct);

        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        var azure = await terminal.WaitUntilAsync(f => ChartsShown(f, "🌐 namespace sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.UpArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.UpArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => f.ContainsText("aren't available for localhost"), AppHarness.Timeout, cancellationToken: Ct);

        app.Metrics.Requested.ShouldBe([new MetricsScope.Namespace()]);
        var choosing = terminal.Frames.SkipWhile(f => !ReferenceEquals(f, emulator)).ToList();
        choosing.ShouldContain(azure);
        choosing.Select(f => (LineOf(f, "🧪 Local Emulator"), LineOf(f, AppHarness.AzureSubscription.Name), LineOf(f, "🌐 sb-orders-dev"))).Distinct().ShouldHaveSingleItem();
        LineOf(azure, "🌐 namespace sb-orders-dev").ShouldBe(LineOf(emulator, "aren't available for localhost"));
        LineOf(azure, "🌐 namespace sb-orders-dev").ShouldBeGreaterThan(LineOf(azure, "[F1] 📬 Queues") + 1);
    }

    [Test]
    public async Task ConnectionPage_PlusZoomsIn_NotIntoTheSubscriptionFilter()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
        await using var terminal = await app.StartAsync(height: 50);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        await terminal.WaitUntilAsync(f => ChartsShown(f, "🌐 namespace sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);

        await Key(terminal, '+');

        var frame = await terminal.WaitUntilAsync(f => ChartsShown(f, "🌐 namespace sb-orders-dev", "-12h"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("🔍 type to filter").ShouldBeTrue(frame.ScreenText);
        app.Metrics.Windows.Last().Interval.ShouldBe(TimeSpan.FromMinutes(15));
    }

    [Test]
    public async Task ConnectionPage_TooShortForThePanel_StillTogglesItWithF12()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.StartAsync(height: 36);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await app.SelectAzureNamespaceAsync(terminal, AppHarness.AzureNamespace);
        await Task.Delay(app.MetricsOptions.Debounce * 2, Ct);
        terminal.CurrentFrame.ContainsText("🌐 namespace sb-orders-dev").ShouldBeFalse(terminal.CurrentFrame.ScreenText);
        terminal.CurrentFrame.ContainsText("[F12] 📈 Metrics").ShouldBeTrue(terminal.CurrentFrame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.F12, '\0', cancellationToken: Ct);
        var connection = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.F1, '\0', cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, connection);
        await terminal.WaitForTextAsync("[F12] 📈 Metrics", AppHarness.Timeout, Ct);
        await Task.Delay(app.MetricsOptions.Debounce * 2, Ct);

        terminal.CurrentFrame.ContainsText("last 24 h").ShouldBeFalse(terminal.CurrentFrame.ScreenText);
        app.Metrics.Requested.ShouldBeEmpty();
    }
}
