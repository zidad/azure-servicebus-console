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
        await terminal.WaitForTextAsync("[F1] 📬 Queues", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(key, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("[F1] 📬 Queues") && f.ContainsText(header), AppHarness.Timeout, cancellationToken: Ct);
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
        app.Namespaces.Subscriptions.Add(new AzureSubscription("Contoso Development", "6f1d2c3b-0000-4000-8000-000000000001"));
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitForTextAsync("Contoso Development", AppHarness.Timeout, Ct);

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
        frame.GetLine(1).ShouldContain("🔑 Contoso Development  ›  🌐 sb-dev-b  ›  📬 Queues (1)");
        frame.GetLine(0).ShouldNotContain("rg-b");

        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await terminal.WaitForTextAsync("[F1] 📬 Queues", AppHarness.Timeout, Ct);
        await terminal.WaitForTextAsync("sb-dev-b", AppHarness.Timeout, Ct);
        app.Connection.CurrentNamespace.ShouldBe("sb-dev-b.servicebus.windows.net");
    }

    [Fact]
    public async Task SubscriptionListingFails_ShowsError_AndEmulatorStillWorks()
    {
        var app = new AppHarness();
        app.Namespaces.SubscriptionsError = new InvalidOperationException("Please run 'az login'");
        await using var terminal = await app.StartAsync();
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.WaitForTextAsync("Could not list Azure subscriptions", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.F1, '\0', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("[F1]") && f.ContainsText("📬 Queues"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Fact]
    public async Task TypingFiltersSubscriptions_AndPicksTheFirstMatch()
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.AddRange(
        [
            new AzureSubscription("Contoso Development", "00000000-0000-4000-8000-000000000001"),
            new AzureSubscription("Contoso Production", "00000000-0000-4000-8000-000000000002"),
            new AzureSubscription("Contoso Sandbox", "00000000-0000-4000-8000-000000000003"),
        ]);
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.WaitForTextAsync("Contoso Sandbox", AppHarness.Timeout, Ct);

        await terminal.TypeAsync("prod", Ct);

        var frame = await terminal.WaitUntilAsync(f => !f.ContainsText("Contoso Sandbox") && !f.ContainsText("Contoso Development"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Contoso Production").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Local Emulator").ShouldBeTrue(frame.ScreenText);
        app.NavState.AzureSubscription?.Name.ShouldBe("Contoso Production");
    }

    [Fact]
    public async Task SubscriptionsWithoutServiceBus_AreHiddenOnceChecked()
    {
        var app = new AppHarness();
        // Billing is listed first, so a frame without it must come from the check, not from listing order.
        app.Namespaces.Subscriptions.AddRange(
        [
            new AzureSubscription("Contoso Billing", "00000000-0000-4000-8000-000000000009"),
            new AzureSubscription("Contoso Development", "00000000-0000-4000-8000-000000000001"),
        ]);
        app.Namespaces.WithoutNamespaces.Add("00000000-0000-4000-8000-000000000009");
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("Contoso Development") && !f.ContainsText("Contoso Billing"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Local Emulator").ShouldBeTrue(frame.ScreenText);
    }

    [Fact]
    public async Task NamespaceChecks_ApplyBeforeTheSubscriptionListFinishes()
    {
        var app = new AppHarness();
        // Billing is listed first, so a frame without it must come from the check, not from listing order.
        app.Namespaces.Subscriptions.AddRange(
        [
            new AzureSubscription("Contoso Billing", "00000000-0000-4000-8000-000000000009"),
            new AzureSubscription("Contoso Development", "00000000-0000-4000-8000-000000000001"),
        ]);
        app.Namespaces.WithoutNamespaces.Add("00000000-0000-4000-8000-000000000009");
        app.Namespaces.HoldListing = new TaskCompletionSource();
        await using var terminal = await app.StartAsync(height: 40);
        await AppHarness.WaitForTableFocusAsync(terminal);

        // Still listing (cached list shown, live refresh pending), yet the empty subscription is already hidden.
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("Contoso Development") && !f.ContainsText("Contoso Billing"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Listing Azure subscriptions").ShouldBeTrue(frame.ScreenText);
        app.Namespaces.HoldListing.SetResult();
    }
}
