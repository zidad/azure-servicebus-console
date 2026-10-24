using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

public sealed class QueueListPageTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    // Already in display order: the page keeps the first-loaded queue selected across re-sorts.
    private static AppHarness WithQueues()
    {
        var app = new AppHarness();
        app.Queues.Queues.AddRange(
        [
            new QueueInfo { Name = "payments", DeadLetterMessageCount = 3 },
            new QueueInfo { Name = "emails", ActiveMessageCount = 1 },
            new QueueInfo { Name = "orders", ActiveMessageCount = 12 },
        ]);
        return app;
    }

    private static int RowOf(TerminalFrame frame, string text)
        => Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains(text));

    [Fact]
    public async Task ShowsQueues_WithDeadLetteredFirst()
    {
        var app = new AppHarness();
        app.Queues.Queues.AddRange(
        [
            new QueueInfo { Name = "orders", ActiveMessageCount = 12 },
            new QueueInfo { Name = "payments", DeadLetterMessageCount = 3 },
            new QueueInfo { Name = "emails", ActiveMessageCount = 1 },
        ]);
        await using var terminal = await app.OpenQueuesAsync();

        var frame = await terminal.WaitForTextAsync("3 queue(s)", AppHarness.Timeout, Ct);

        frame.ContainsText("! 3").ShouldBeTrue(frame.ScreenText);
        RowOf(frame, "payments").ShouldBeLessThan(RowOf(frame, "emails"));
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));
    }

    [Fact]
    public async Task TypingFiltersTheList()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("3 queue(s)", AppHarness.Timeout, Ct);

        await terminal.TypeAsync("pay", Ct);

        var frame = await terminal.WaitForTextAsync("1 queue(s)", AppHarness.Timeout, Ct);
        frame.ContainsText("payments").ShouldBeTrue();
        frame.ContainsText("orders").ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsksForConfirmation_ThenDeletesSelectedQueue()
    {
        var app = WithQueues();
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("3 queue(s)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Delete queue 'payments'?", AppHarness.Timeout, Ct);
        app.Queues.Deleted.ShouldBeEmpty();

        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await terminal.WaitForTextAsync("2 queue(s)", AppHarness.Timeout, Ct);
        app.Queues.Deleted.ShouldBe(["payments"]);
    }

    [Fact]
    public async Task DeclinedDelete_KeepsQueue()
    {
        var app = WithQueues();
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("3 queue(s)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Delete queue 'payments'?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.N, 'n', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Delete queue"), AppHarness.Timeout, cancellationToken: Ct);
        app.Queues.Deleted.ShouldBeEmpty();
    }

    [Fact]
    public async Task LoadFailure_ShowsRetryHint()
    {
        var app = new AppHarness();
        app.Queues.LoadError = new InvalidOperationException("boom");
        await using var terminal = await app.OpenQueuesAsync();

        await terminal.WaitForTextAsync("Failed to load. Press [F5] to retry.", AppHarness.Timeout, Ct);
    }

    [Fact]
    public async Task EnterOpensSelectedQueue()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("3 queue(s)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("queue(s)") && f.ContainsText("payments"), AppHarness.Timeout, cancellationToken: Ct);
    }
}
