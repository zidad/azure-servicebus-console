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
        await terminal.WaitUntilAsync(f => f.ContainsText("▸ 🌐 sb-orders-dev") && f.ContainsText("▸ 🌐 sb-legacy") && !f.ContainsText("Discovering"),
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
        frame.ContainsText("└─ ▸ 🌐 localhost").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("▾ 🔑 Contoso Development").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("├─ ▸ 🌐 sb-legacy").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task RightArrow_ShowsTheNamespaceSections_AndEnterOpensOne()
    {
        var app = Harness();
        await using var terminal = await OpenTreeAsync(app);

        await Down(terminal, 3);
        await terminal.SendKeyAsync(ConsoleKey.RightArrow, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("├─   📢 Topics", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("└─   📥 All Subscriptions", AppHarness.Timeout, Ct);

        await Down(terminal, 1);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(0).ShouldContain("🔑 Contoso Development  ›  🌐 sb-orders-dev  ›  📬 Queues (1)");
    }

    [Test]
    public async Task NamespaceWithoutTopics_HasQueuesOnly()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        await Down(terminal, 2);
        await terminal.SendKeyAsync(ConsoleKey.RightArrow, '\0', cancellationToken: Ct);
        // Under sb-legacy, which is not the last namespace: a lone └─ means Queues is its only section.
        var frame = await terminal.WaitForTextAsync("│  └─   📬 Queues", AppHarness.Timeout, Ct);
        frame.ContainsText("│  ├─").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task Typing_FiltersTheTree_KeepingAncestors()
    {
        await using var terminal = await OpenTreeAsync(Harness());

        foreach (var c in "/legacy") await terminal.SendKeyAsync(ConsoleKey.A, c, cancellationToken: Ct);
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("🔍 legacy") && !f.ContainsText("sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("└─ ▸ 🌐 sb-legacy").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("▾ 🔑 Contoso Development").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Local Emulator").ShouldBeFalse(frame.ScreenText);
    }
}
