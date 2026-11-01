using System.Collections.Concurrent;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(StartupTests))]
public sealed class StartupTests
{
    private const string QueueHeader = "🔑 Contoso Development  ›  🌐 sb-orders-dev  ›  📬 Queues (1)";

    private static AppHarness WithAzure(StartupTarget startup)
    {
        var app = new AppHarness();
        app.Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
        app.Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
        app.Queues.Queues.Add(new QueueInfo { Name = "orders" });
        app.NavState.Startup = startup;
        return app;
    }

    [Test]
    public async Task SubscriptionAndNamespace_OpenItsQueues()
    {
        var app = WithAzure(new("Contoso Development", "sb-orders-dev"));
        await using var terminal = await app.StartAsync(height: 40);

        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        frame.GetLine(1).ShouldContain(QueueHeader);
        app.Connection.CurrentNamespace.ShouldBe(AppHarness.AzureNamespace.FullyQualifiedNamespace);
    }

    [Test]
    public async Task OpensAsSoonAsListed_WhileLiveListingsAreStillRunning()
    {
        var app = WithAzure(new("Contoso Development", "sb-orders-dev"));
        app.Namespaces.HoldListing = new TaskCompletionSource();
        app.Namespaces.HoldNamespaces = new TaskCompletionSource();
        await using var terminal = await app.StartAsync(height: 40);

        var frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        frame.GetLine(1).ShouldContain(QueueHeader);
        app.Namespaces.HoldListing.SetResult();
        app.Namespaces.HoldNamespaces.SetResult();
    }

    [Test]
    public async Task EmulatorByName_OpensItsQueues()
    {
        var app = WithAzure(new("emulator", "localhost"));
        await using var terminal = await app.StartAsync();

        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        app.Connection.IsEmulator.ShouldBeTrue();
    }

    [Test]
    public async Task OpenedQueues_ShowWhileTheyLoad()
    {
        // The emulator matches during the first render; the app's first frame waits for every page shown by then.
        var app = WithAzure(new("emulator", "localhost"));
        app.Queues.HoldLoads = new TaskCompletionSource();
        await using var terminal = await app.StartAsync().WaitAsync(AppHarness.Timeout, Ct);

        await terminal.WaitForTextAsync("Loading queues...", AppHarness.Timeout, Ct);
        app.Queues.HoldLoads.SetResult();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
    }

    [Test]
    public async Task SubscriptionAlone_SelectsIt_AndFocusesItsNamespaces()
    {
        var app = WithAzure(new("contoso development"));
        // Listed once the focus session runs: FocusManager.BeginSession drops a request made after the first frame, before it.
        app.Namespaces.NamespacesGate = new TaskCompletionSource();
        await using var terminal = await app.StartAsync(height: 40);
        app.Namespaces.NamespacesGate.SetResult();

        await AppHarness.WaitForFocusAsync(terminal, focusOrder: "3");
        var frame = await terminal.WaitForTextAsync("[F1] 📬 Queues", AppHarness.Timeout, Ct);
        frame.ContainsText("sb-orders-dev").ShouldBeTrue(frame.ScreenText);
        app.NavState.AzureSubscription.ShouldBe(AppHarness.AzureSubscription);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        frame = await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        frame.GetLine(1).ShouldContain(QueueHeader);
    }

    [Test]
    public async Task UnknownNamespace_StaysOnTheConnectionPage_AndSaysSo()
    {
        var app = WithAzure(new("Contoso Development", "sb-missing"));
        await using var terminal = await app.StartAsync(height: 40);

        var frame = await terminal.WaitForTextAsync("Namespace \"sb-missing\" not found in Contoso Development.", AppHarness.Timeout, Ct);

        frame.ContainsText("sb-orders-dev").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("[F1] 📬 Queues").ShouldBeTrue(frame.ScreenText);
        app.NavState.AzureSubscription.ShouldBe(AppHarness.AzureSubscription);
    }

    [Test]
    public async Task UnknownSubscription_SelectsTheEmulator_AndSaysSo()
    {
        var app = WithAzure(new("Contoso Production", "sb-orders-dev"));
        await using var terminal = await app.StartAsync(height: 40);

        var frame = await terminal.WaitForTextAsync("Azure subscription \"Contoso Production\" not found.", AppHarness.Timeout, Ct);

        frame.ContainsText("[F1] 📬 Queues").ShouldBeTrue(frame.ScreenText);
        app.NavState.AzureSubscription.ShouldBe(AzureSubscription.Emulator);
    }

    [Test]
    public async Task Startup_AppliesToTheFirstVisitOnly()
    {
        var app = WithAzure(new("Contoso Development", "sb-orders-dev"));
        await using var terminal = await app.StartAsync(height: 40);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        var queuesFocus = terminal.FocusKey;
        ConcurrentQueue<string> visited = [];
        app.Navigation.LocationChanged += (_, e) => visited.Enqueue(app.Navigation.ToBaseRelativePath(e.Location));

        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queuesFocus);
        await terminal.WaitUntilAsync(f => f.ContainsText("[F1] 📬 Queues") && f.ContainsText("sb-orders-dev"), AppHarness.Timeout, cancellationToken: Ct);
        // The namespace is listed again; a startup target still applying would open the queues now.
        await Task.Delay(TimeSpan.FromMilliseconds(500), Ct);

        visited.ShouldBe([""]);
        terminal.CurrentFrame.ContainsText("[F1] 📬 Queues").ShouldBeTrue(terminal.CurrentFrame.ScreenText);
    }
}
