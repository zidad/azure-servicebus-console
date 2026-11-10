using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(ConnectionTreeTests))]
public sealed class ConnectionTreeTests
{
    private static readonly NamespaceInfo Basic = AppHarness.AzureNamespace with
    {
        Name = "sb-legacy", FullyQualifiedNamespace = "sb-legacy.servicebus.windows.net", Sku = "Basic",
    };

    private static AppHarness Harness()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.AddRange([AppHarness.AzureNamespace, Basic]);
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        return app;
    }

    private static async Task<HeadlessTerminal> OpenTreeAsync(AppHarness app)
    {
        var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitUntilAsync(f => f.ContainsText("🌐 sb-orders-dev") && f.ContainsText("🌐 sb-legacy") && !f.ContainsText("Discovering"),
            AppHarness.Timeout, cancellationToken: Ct);
        return terminal;
    }

    // Rows: emulator, localhost (selected), Contoso Development, sb-legacy, sb-orders-dev.
    private static async Task Down(HeadlessTerminal terminal, int times)
    {
        foreach (var _ in Enumerable.Range(0, times)) await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
    }

    [Test]
    public async Task Subscriptions_AreOpen_WithTheirNamespaces()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        var frame = terminal.CurrentFrame;
        frame.ContainsText("▾ 🧪 Local Emulator").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("└─ 🌐 localhost").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("▾ 🔑 Contoso Development").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("├─ 🌐 sb-legacy").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task Namespaces_HaveNoSectionRows()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        await Down(terminal, 3);
        await terminal.SendKeyAsync(ConsoleKey.RightArrow, '\0', cancellationToken: Ct);

        // Section rows would hang under a namespace: "├─ 📬 Queues"; the footer's key hints have no tree guide.
        var frame = terminal.CurrentFrame;
        foreach (var icon in new[] { Icons.Queue, Icons.Topic, Icons.Subscription })
        {
            frame.ContainsText($"─ {icon}").ShouldBeFalse(frame.ScreenText);
            frame.ContainsText($"─   {icon}").ShouldBeFalse(frame.ScreenText);
        }
        frame.ContainsText("▸ 🌐").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    [Arguments(3, "sb-orders-dev")]
    [Arguments(2, "sb-legacy")]
    public async Task Enter_OnANamespace_OpensItsQueues(int down, string name)
    {
        await using var terminal = await OpenTreeAsync(Harness());

        await Down(terminal, down);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(0).ShouldContain($"🔑 Contoso Development  ›  🌐 {name}  ›  📬 Queues (1)");
    }

    [Test]
    public async Task Typing_FiltersTheTree_KeepingAncestors()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        foreach (var c in "/legacy") await terminal.SendKeyAsync(ConsoleKey.A, c, cancellationToken: Ct);
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("🔍 legacy") && !f.ContainsText("sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("└─ 🌐 sb-legacy").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("▾ 🔑 Contoso Development").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Local Emulator").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task OpeningANamespace_ListsItUnderRecent_WithWhenItWasOpened()
    {
        var app = Harness();
        app.Time = new FixedTime(new DateTimeOffset(2026, 11, 10, 9, 0, 0, TimeSpan.Zero));
        await using var terminal = await OpenTreeAsync(app);
        terminal.CurrentFrame.ContainsText("Recent").ShouldBeFalse(terminal.CurrentFrame.ScreenText);
        var connectionFocus = terminal.FocusKey;

        await Down(terminal, 3);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        ((FixedTime)app.Time).Now += TimeSpan.FromHours(2);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("▾ 🕘 Recent"), AppHarness.Timeout, cancellationToken: Ct);
        var lines = Enumerable.Range(0, frame.Height).Select(frame.GetLine).ToList();
        var recent = lines.FindIndex(l => l.Contains("▾ 🕘 Recent"));
        lines[recent + 1].ShouldContain("└─ 🌐 sb-orders-dev Contoso Development");
        lines[recent + 1].TrimEnd('│', ' ').ShouldEndWith("✅ Active      2 h ago");
        lines[recent + 2].ShouldContain("▾ 🧪 Local Emulator");
        lines.Single(l => l.Contains("─ 🌐 sb-orders-dev") && !l.Contains("Contoso")).TrimEnd('│', ' ').ShouldEndWith("✅ Active      2 h ago");
        lines.Single(l => l.Contains("─ 🌐 sb-legacy")).ShouldNotContain("ago");
        frame.ContainsText("Opened").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task ARecentNamespace_InAnotherSubscription_OpensThere()
    {
        var app = Harness();
        await app.Recent.RecordAsync(AppHarness.AzureSubscription, Basic);
        await using var terminal = await OpenTreeAsync(app);
        await terminal.WaitForTextAsync("▾ 🕘 Recent", AppHarness.Timeout, Ct);

        // Rows: Recent, sb-legacy (recent), emulator, localhost (selected).
        foreach (var _ in Enumerable.Range(0, 2)) await terminal.SendKeyAsync(ConsoleKey.UpArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => app.Connection.CurrentNamespace == Basic.FullyQualifiedNamespace, AppHarness.Timeout, cancellationToken: Ct);
        app.NavState.AzureSubscription.ShouldBe(AppHarness.AzureSubscription);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(0).ShouldContain("🔑 Contoso Development  ›  🌐 sb-legacy  ›  📬 Queues (1)");
    }

    [Test]
    public async Task CollapsedSubscriptions_ShowTheirNamespaceCount()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        // From localhost: its subscription, collapse it; then Contoso Development, collapse it.
        await terminal.SendKeyAsync(ConsoleKey.LeftArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.LeftArrow, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("▸ 🧪 Local Emulator", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.LeftArrow, '\0', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("▸ 🔑 Contoso Development", AppHarness.Timeout, Ct);
        var lines = Enumerable.Range(0, frame.Height).Select(frame.GetLine).ToList();
        lines.Single(l => l.Contains("Local Emulator")).ShouldContain("1 namespace ");
        lines.Single(l => l.Contains("Contoso Development")).ShouldContain("2 namespaces");
        lines.Single(l => l.Contains("Contoso Development")).ShouldNotContain("6f1d2c3b");
    }
}
