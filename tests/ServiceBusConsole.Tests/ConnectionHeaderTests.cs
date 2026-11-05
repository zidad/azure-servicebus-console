using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(ConnectionHeaderTests))]
public sealed class ConnectionHeaderTests
{
    [Test]
    public async Task DropdownTree_SwitchesNamespace_AndReloadsTheSection()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.Add(new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe" });
        app.Namespaces.Subscriptions.Add(new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var loadsBefore = app.Queues.Loads;

        await AppHarness.ClickTextAsync(terminal, "Local Emulator");
        // Namespaces and localhost's sections load in the background; rows inserted above sb-dev-a would move it under the click.
        await terminal.WaitUntilAsync(f => f.ContainsText("Go to") && f.ContainsText("▾ 🌐 localhost") && f.ContainsText("📥 All Subscriptions") && f.ContainsText("sb-dev-a"),
            AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.ClickTextAsync(terminal, "sb-dev-a");
        await AppHarness.ClickSettledAsync(terminal, f => QueuesRowBelow(f, "▾ 🌐 sb-dev-a") is { } y ? (f.GetLine(y).IndexOf("📬", StringComparison.Ordinal), y) : null);

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("🔑 Contoso Development  ›  🌐 sb-dev-a  ›  📬 Queues (1)"), AppHarness.Timeout, cancellationToken: Ct);
        app.Connection.CurrentNamespace.ShouldBe("sb-dev-a.servicebus.windows.net");
        app.Queues.Loads.ShouldBeGreaterThan(loadsBefore);
    }

    [Test]
    public async Task Escape_ClosesThePicker_WithoutSwitching()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "localhost");
        await terminal.WaitForTextAsync("Go to", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Go to"), AppHarness.Timeout, cancellationToken: Ct);
        app.Connection.CurrentNamespace.ShouldBe("localhost");
    }

    [Test]
    public async Task SectionDropdown_SwitchesFromQueuesToTopics()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Queues (1)");
        await terminal.WaitForTextAsync("Go to", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "├─   📢 Topics");

        await terminal.WaitForTextAsync("Topics (1)", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("order-events", AppHarness.Timeout, Ct);
    }

    // The section rows appear a render after the namespace expands.
    private static int? QueuesRowBelow(TerminalFrame frame, string text) =>
        Enumerable.Range(0, frame.Height - 1)
            .Where(i => frame.GetLine(i).Contains(text, StringComparison.Ordinal) && frame.GetLine(i + 1).Contains("📬 Queues", StringComparison.Ordinal))
            .Select(i => (int?)(i + 1))
            .FirstOrDefault();
}
