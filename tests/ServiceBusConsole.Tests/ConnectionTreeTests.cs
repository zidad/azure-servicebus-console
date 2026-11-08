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
}
