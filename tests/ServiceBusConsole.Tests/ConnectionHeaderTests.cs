namespace ServiceBusConsole.Tests;

public sealed class ConnectionHeaderTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task PickingSubscriptionThenNamespace_SwitchesAndReloadsTheSection()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.Add(new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe" });
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var loadsBefore = app.Queues.Loads;

        await AppHarness.ClickTextAsync(terminal, "Local Emulator");
        await terminal.WaitForTextAsync("Azure subscription", AppHarness.Timeout, Ct);
        // First click moves the highlight, the second picks the row.
        await AppHarness.ClickTextAsync(terminal, "dev ");
        await AppHarness.ClickTextAsync(terminal, "dev ");

        await terminal.WaitForTextAsync("Namespaces in dev", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("sb-dev-a", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => f.GetLine(1).Contains("dev  ›  sb-dev-a  ›  Queues (1)"), AppHarness.Timeout, cancellationToken: Ct);
        app.Connection.CurrentNamespace.ShouldBe("sb-dev-a.servicebus.windows.net");
        app.Queues.Loads.ShouldBeGreaterThan(loadsBefore);
    }

    [Fact]
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
}
