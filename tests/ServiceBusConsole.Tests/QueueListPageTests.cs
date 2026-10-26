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

        var frame = await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        frame.GetLine(RowOf(frame, "payments")).ShouldNotContain("💀");
        frame.GetLine(RowOf(frame, "payments")).ShouldContain(" 3 ");
        frame.ContainsText("💀 DLQ").ShouldBeTrue(frame.ScreenText);
        frame.GetLine(1).ShouldContain("🧪 Local Emulator  ›  🌐 localhost  ›  📬 Queues (3)");
        RowOf(frame, "payments").ShouldBeLessThan(RowOf(frame, "emails"));
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));
    }

    [Fact]
    public async Task TypingFiltersTheList()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await terminal.TypeAsync("pay", Ct);

        var frame = await terminal.WaitForTextAsync("Queues (1 of 3)", AppHarness.Timeout, Ct);
        frame.ContainsText("payments").ShouldBeTrue();
        frame.ContainsText("orders").ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsksForConfirmation_ThenDeletesSelectedQueue()
    {
        var app = WithQueues();
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Delete queue 'payments'?", AppHarness.Timeout, Ct);
        app.Queues.Deleted.ShouldBeEmpty();

        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Queues (2)", AppHarness.Timeout, Ct);
        app.Queues.Deleted.ShouldBe(["payments"]);
    }

    [Fact]
    public async Task DeclinedDelete_KeepsQueue()
    {
        var app = WithQueues();
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

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
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Queues (") && f.ContainsText("payments"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Fact]
    public async Task ClickingHeader_CyclesAscendingDescendingOff()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Active");
        var frame = await terminal.WaitForTextAsync("Active ▲", AppHarness.Timeout, Ct);
        RowOf(frame, "payments").ShouldBeLessThan(RowOf(frame, "emails"));
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));

        await AppHarness.ClickTextAsync(terminal, "Active ▲");
        frame = await terminal.WaitForTextAsync("Active ▼", AppHarness.Timeout, Ct);
        RowOf(frame, "orders").ShouldBeLessThan(RowOf(frame, "emails"));
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "payments"));

        // Off: no column sorted, neutral order by name.
        await AppHarness.ClickTextAsync(terminal, "Active ▼");
        frame = await terminal.WaitUntilAsync(f => f.ContainsText("Active") && !f.ContainsText("▲") && !f.ContainsText("▼"), AppHarness.Timeout, cancellationToken: Ct);
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));
        RowOf(frame, "orders").ShouldBeLessThan(RowOf(frame, "payments"));
    }

    [Fact]
    public async Task DefaultDlqSort_ShowsInHeader_AndClickTurnsItOff()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        var frame = await terminal.WaitForTextAsync("DLQ ▼", AppHarness.Timeout, Ct);
        RowOf(frame, "payments").ShouldBeLessThan(RowOf(frame, "emails"));

        await AppHarness.ClickTextAsync(terminal, "DLQ ▼");

        frame = await terminal.WaitUntilAsync(f => !f.ContainsText("▲") && !f.ContainsText("▼"), AppHarness.Timeout, cancellationToken: Ct);
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));
        RowOf(frame, "orders").ShouldBeLessThan(RowOf(frame, "payments"));
    }

    [Fact]
    public async Task ClickingAnotherHeader_StartsAscendingOnThatColumn()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Active");
        await terminal.WaitForTextAsync("Active ▲", AppHarness.Timeout, Ct);
        await AppHarness.ClickTextAsync(terminal, "Queue ");

        var frame = await terminal.WaitForTextAsync("Queue ▲", AppHarness.Timeout, Ct);
        frame.ContainsText("Active ▲").ShouldBeFalse();
        RowOf(frame, "emails").ShouldBeLessThan(RowOf(frame, "orders"));
        RowOf(frame, "orders").ShouldBeLessThan(RowOf(frame, "payments"));
    }

    [Fact]
    public async Task Sorting_KeepsSelectedQueue()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await AppHarness.ClickTextAsync(terminal, "Active");
        await AppHarness.ClickTextAsync(terminal, "Active ▲");
        await terminal.WaitForTextAsync("Active ▼", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.F9, '\0', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Delete queue 'payments'?", AppHarness.Timeout, Ct);
    }

    [Fact]
    public async Task FilterBox_IsNotStyledFocused_WhenTheTableHasFocus()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        // The filter is the first focusable element while the queues load; once the table takes focus,
        // its border must go back to the unfocused color.
        await AppHarness.WaitForTableFocusAsync(terminal);
        await Task.Delay(300, Ct);
        FilterBorder(terminal.CurrentFrame).ShouldNotBe(Spectre.Console.Color.Yellow);
    }

    private static Spectre.Console.Color? FilterBorder(TerminalFrame frame)
    {
        for (var y = frame.Height - 1; y >= 0; y--)
        {
            if (frame[0, y].Text == "╭") return frame[0, y].Style?.Foreground;
        }
        return null;
    }
}
