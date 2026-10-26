namespace ServiceBusConsole.Tests;

public sealed class ConnectionPageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Theory]
    [InlineData(ConsoleKey.F1, "Queues")]
    [InlineData(ConsoleKey.F2, "Topics")]
    [InlineData(ConsoleKey.F3, "All Subscriptions")]
    public async Task FunctionKey_WithEmulatorPreselected_OpensPage(ConsoleKey key, string header)
    {
        await using var terminal = await new AppHarness().StartAsync();
        await terminal.WaitForTextAsync("[F1] Queues", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(key, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("[F1] Queues") && f.ContainsText(header), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Fact]
    public async Task AzureSubscription_ListsNamespacesWithDetails_AndF1OpensHighlightedOne()
    {
        var app = new AppHarness();
        app.Namespaces.Namespaces.AddRange(
        [
            new NamespaceInfo { Name = "sb-dev-a", FullyQualifiedNamespace = "sb-dev-a.servicebus.windows.net", ResourceGroup = "rg-a", Location = "West Europe", Sku = "Standard", Status = "Active" },
            new NamespaceInfo { Name = "sb-dev-b", FullyQualifiedNamespace = "sb-dev-b.servicebus.windows.net", ResourceGroup = "rg-b", Location = "North Europe", Sku = "Premium", Status = "Active" },
        ]);
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        var frame = await terminal.WaitForTextAsync("sb-dev-b", AppHarness.Timeout, Ct);
        frame.ContainsText("Premium").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("North Europe").ShouldBeTrue(frame.ScreenText);

        var subscriptionsFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != subscriptionsFocus, AppHarness.Timeout, cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => app.Connection.CurrentNamespace == "sb-dev-b.servicebus.windows.net", AppHarness.Timeout, cancellationToken: Ct);

        await terminal.SendKeyAsync(ConsoleKey.F1, '\0', cancellationToken: Ct);
        frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(1).ShouldContain("dev  ›  sb-dev-b  ›  Queues (1)   │  rg-b  ·  North Europe  ·  Premium  ·  Active");

        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await terminal.WaitForTextAsync("[F1] Queues", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("sb-dev-b", AppHarness.Timeout, Ct);
        app.Connection.CurrentNamespace.ShouldBe("sb-dev-b.servicebus.windows.net");
    }
}
