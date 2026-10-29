namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(ConnectionHeaderTests))]
public sealed class ConnectionHeaderTests
{
    [Test]
    public async Task ClickingSubscriptionThenNamespace_SwitchesAndReloadsTheSection()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.Add(new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe" });
        app.Namespaces.Subscriptions.Add(new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var loadsBefore = app.Queues.Loads;

        await AppHarness.ClickTextAsync(terminal, "Local Emulator");
        await terminal.WaitForTextAsync("Azure subscription", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "Contoso Development");

        await terminal.WaitForTextAsync("Namespaces in Contoso Development", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "sb-dev-a");

        await terminal.WaitUntilAsync(f => f.GetLine(1).Contains("🔑 Contoso Development  ›  🌐 sb-dev-a  ›  📬 Queues (1)"), AppHarness.Timeout, cancellationToken: Ct);
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
        await terminal.WaitForTextAsync("Namespaces in Local Emulator", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Namespaces in"), AppHarness.Timeout, cancellationToken: Ct);
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
        await AppHarness.ClickTextAsync(terminal, "Topics");

        await terminal.WaitForTextAsync("Topics (1)", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("order-events", AppHarness.Timeout, Ct);
    }
}
