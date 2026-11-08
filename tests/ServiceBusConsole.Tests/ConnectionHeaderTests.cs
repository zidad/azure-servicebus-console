using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(ConnectionHeaderTests))]
public sealed class ConnectionHeaderTests
{
    [Test]
    public async Task DropdownTree_SwitchesNamespace_OnTheSameList()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.Add(new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe" });
        app.Namespaces.Subscriptions.Add(new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var loadsBefore = app.Queues.Loads;

        await AppHarness.ClickTextAsync(terminal, "Local Emulator");
        await terminal.WaitUntilAsync(f => f.ContainsText("Go to") && f.ContainsText("🌐 sb-dev-a"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.ClickTextAsync(terminal, "🌐 sb-dev-a");

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
    public async Task DropdownTree_OnTopics_SwitchesToANamespaceWithTopics_StaysOnTopics()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.Add(new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe" });
        app.Namespaces.Subscriptions.Add(new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.SendKeyAsync(ConsoleKey.D2, '2', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Topics (1)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Local Emulator");
        await terminal.WaitUntilAsync(f => f.ContainsText("Go to") && f.ContainsText("🌐 sb-dev-a"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.ClickTextAsync(terminal, "🌐 sb-dev-a");

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("🌐 sb-dev-a  ›  📢 Topics (1)"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task SectionCrumb_DeeperIn_GoesBackToItsList()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("orders"), AppHarness.Timeout, cancellationToken: Ct);

        await AppHarness.ClickTextAsync(terminal, "📬 Queues");

        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        terminal.CurrentFrame.ContainsText("Go to").ShouldBeFalse();
    }
}
