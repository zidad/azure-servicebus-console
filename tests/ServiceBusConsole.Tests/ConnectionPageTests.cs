namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(ConnectionPageTests))]
public sealed class ConnectionPageTests
{
    [Test]
    [Arguments(ConsoleKey.F1, "Queues")]
    [Arguments(ConsoleKey.F2, "Topics")]
    [Arguments(ConsoleKey.F3, "All Subscriptions")]
    public async Task FunctionKey_WithEmulatorPreselected_OpensPage(ConsoleKey key, string header)
    {
        await using var terminal = await new AppHarness().StartAsync();
        await terminal.WaitForTextAsync("1  📬 Queues", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(key, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("1  📬 Queues") && f.ContainsText(header), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task AzureSubscription_ListsNamespacesWithDetails_AndF1OpensHighlightedOne()
    {
        var app = new AppHarness();
        app.Catalog.Brokers.AddRange(
        [
            new BrokerInfo { Name = "sb-dev-a", Address = "sb-dev-a.servicebus.windows.net", Properties = ServiceBusProperties(sku: "Standard", location: "West Europe", resourceGroup: "rg-a", status: "Active") },
            new BrokerInfo { Name = "sb-dev-b", Address = "sb-dev-b.servicebus.windows.net", Properties = ServiceBusProperties(sku: "Premium", location: "North Europe", resourceGroup: "rg-b", status: "Active") },
        ]);
        app.Catalog.Connections.Add(new ConnectionInfo("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await app.SelectAzureNamespaceAsync(terminal, app.Catalog.Brokers[1]);
        var frame = terminal.CurrentFrame;
        frame.ContainsText("Premium   North Europe  rg-b            ✅ Active").ShouldBeTrue(frame.ScreenText);
        var connectionFocus = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.F1, '\0', cancellationToken: Ct);
        frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(0).ShouldContain("🔑 Contoso Development  ›  🌐 sb-dev-b  ›  📬 Queues (1)");
        frame.GetLine(0).ShouldNotContain("rg-b");

        await AppHarness.WaitForTableFocusAsync(terminal, connectionFocus);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await terminal.WaitForTextAsync("1  📬 Queues", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("sb-dev-b", AppHarness.Timeout, Ct);
        app.Current.Address.ShouldBe("sb-dev-b.servicebus.windows.net");
    }

    [Test]
    public async Task SubscriptionListingFails_ShowsError_AndEmulatorStillWorks()
    {
        var app = new AppHarness();
        app.Catalog.ConnectionsError = new InvalidOperationException("Please run 'az login'");
        await using var terminal = await app.StartAsync();
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.WaitForTextAsync("Could not list Azure subscriptions", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.F1, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("1  📬 Queues") && f.ContainsText("📬 Queues"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task TypingFiltersTheTree_AndPicksTheFirstMatch()
    {
        var app = new AppHarness();
        app.Catalog.Connections.AddRange(
        [
            new ConnectionInfo("Contoso Development", "00000000-0000-4000-8000-000000000001"),
            new ConnectionInfo("Contoso Production", "00000000-0000-4000-8000-000000000002"),
            new ConnectionInfo("Contoso Sandbox", "00000000-0000-4000-8000-000000000003"),
        ]);
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitForTextAsync("Contoso Sandbox", AppHarness.Timeout, Ct);

        await terminal.TypeAsync("/prod", Ct);

        var frame = await terminal.WaitUntilAsync(f => !f.ContainsText("Contoso Sandbox") && !f.ContainsText("Contoso Development"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Contoso Production").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Local Emulator").ShouldBeFalse(frame.ScreenText);
        await terminal.WaitUntilAsync(_ => app.NavState.Connection?.Name == "Contoso Production", AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task SubscriptionsWithoutServiceBus_AreHiddenOnceChecked()
    {
        var app = new AppHarness();
        // Billing is listed first, so a frame without it must come from the check, not from listing order.
        app.Catalog.Connections.AddRange(
        [
            new ConnectionInfo("Contoso Billing", "00000000-0000-4000-8000-000000000009"),
            new ConnectionInfo("Contoso Development", "00000000-0000-4000-8000-000000000001"),
        ]);
        app.Catalog.WithoutBrokers.Add("00000000-0000-4000-8000-000000000009");
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("Contoso Development") && !f.ContainsText("Contoso Billing"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Local Emulator").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task NamespaceChecks_ApplyBeforeTheSubscriptionListFinishes()
    {
        var app = new AppHarness();
        // Billing is listed first, so a frame without it must come from the check, not from listing order.
        app.Catalog.Connections.AddRange(
        [
            new ConnectionInfo("Contoso Billing", "00000000-0000-4000-8000-000000000009"),
            new ConnectionInfo("Contoso Development", "00000000-0000-4000-8000-000000000001"),
        ]);
        app.Catalog.WithoutBrokers.Add("00000000-0000-4000-8000-000000000009");
        app.Catalog.HoldListing = new TaskCompletionSource();
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        // Still listing (cached list shown, live refresh pending), yet the empty subscription is already hidden.
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("Contoso Development") && !f.ContainsText("Contoso Billing"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Listing Azure subscriptions").ShouldBeTrue(frame.ScreenText);
        app.Catalog.HoldListing.SetResult();
    }
}
