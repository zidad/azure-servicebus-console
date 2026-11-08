namespace ServiceBusConsole.Tests;

/// <summary>What a page offers follows the features of the namespace it shows, not which kind of namespace it is.</summary>
[NotInParallel(nameof(NamespaceFeatureTests))]
public sealed class NamespaceFeatureTests
{
    private static readonly NamespaceInfo Basic = AppHarness.AzureNamespace with
    {
        Name = "sb-legacy", FullyQualifiedNamespace = "sb-legacy.servicebus.windows.net", Sku = "Basic",
    };

    [Test]
    public async Task WithoutDeletion_NoDeleteButton_AndF9DoesNothing()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.ContainsText("P  🧹 Purge DLQ").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("❌ Delete").ShouldBeFalse(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);
        await Task.Delay(300, Ct);

        terminal.CurrentFrame.ContainsText("Delete queue").ShouldBeFalse(terminal.CurrentFrame.ScreenText);
        app.Queues.Deleted.ShouldBeEmpty();
    }

    [Test]
    public async Task WithDeletion_DeleteButtonAndF9()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1, height: 30);
        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.ContainsText("D  ❌ Delete").ShouldBeTrue(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Delete queue 'orders'?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.N, 'n', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => !f.ContainsText("Delete queue"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(ConsoleKey.D, 'D', ConsoleModifiers.Shift, Ct);

        await terminal.WaitForTextAsync("Delete queue 'orders'?", AppHarness.Timeout, Ct);
    }

    [Test]
    public async Task QuestionMark_ListsThePageKeys_AndEscapeClosesThem()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1, height: 40);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var table = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Oem2, '?', ConsoleModifiers.Shift, Ct);

        var help = await terminal.WaitForTextAsync("❓ Keys", AppHarness.Timeout, Ct);
        help.ContainsText("F9 Delete \\").ShouldBeTrue(help.ScreenText);
        help.ContainsText("Shift marks the keys").ShouldBeTrue(help.ScreenText);
        await AppHarness.WaitForFocusAsync(terminal, "1", table);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => !f.ContainsText("❓ Keys"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        terminal.CurrentFrame.ContainsText("Queues (1)").ShouldBeTrue();
    }

    [Test]
    public async Task WithoutTopics_ConnectionPageOffersQueuesOnly()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(Basic);
        await using var terminal = await app.StartAsync();
        await AppHarness.WaitForTableFocusAsync(terminal);
        await app.SelectAzureNamespaceAsync(terminal, Basic);

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("1  📬 Queues") && f.ContainsText("📈 Metrics"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("2  📢 Topics").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("3  📥 All Subscriptions").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("m  📈 Metrics").ShouldBeTrue(frame.ScreenText);
        app.Connection.Current.ShouldBeOfType<FakeBasicNamespace>();
    }

    [Test]
    public async Task SwitchingFromTopicsToANamespaceWithoutThem_OpensItsQueues()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.Topics.Topics.Add(new TopicInfo { Name = "order-events" });
        app.Namespaces.Namespaces.Add(Basic);
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F2, height: 30);
        await terminal.WaitForTextAsync("Topics (1)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "sb-orders-dev");
        await terminal.WaitForTextAsync("Go to", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "🌐 sb-legacy");

        await terminal.WaitUntilAsync(f => f.GetLine(0).Contains("🌐 sb-legacy  ›  📬 Queues (1)"), AppHarness.Timeout, cancellationToken: Ct);
        app.Connection.Current.ShouldBeOfType<FakeBasicNamespace>();
    }

    [Test]
    public async Task SwitchingNamespace_DisposesThePreviousOne()
    {
        var app = new AppHarness();
        var opened = new List<FakeNamespace>();
        app.OpenNamespace = info =>
        {
            var ns = FakeNamespace.For(info, app.Services);
            opened.Add(ns);
            return ns;
        };
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenAzureAsync(ConsoleKey.F1, height: 30);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        opened.Select(ns => (ns.Info.Name, ns.Disposed)).ShouldBe([("localhost", true), ("sb-orders-dev", false)]);
    }
}
