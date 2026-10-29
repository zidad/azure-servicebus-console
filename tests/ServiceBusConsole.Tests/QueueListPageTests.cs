using RazorConsole.Testing;
using Spectre.Console;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(QueueListPageTests))]
public sealed class QueueListPageTests
{
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

    [Test]
    public async Task ChangedCounts_ShowUpWithoutRefresh_HighlightedUntilTheyFade()
    {
        var app = WithQueues();
        app.LiveCounts.Interval = TimeSpan.FromMilliseconds(50);
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        app.Queues.Update("emails", q => q with { ActiveMessageCount = 7 });
        app.Queues.Add(new QueueInfo { Name = "refunds" });

        var frame = await terminal.WaitUntilAsync(f => f.GetLine(RowOf(f, "emails")).Contains(" 7 ") && f.ContainsText("refunds"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Queues (4)").ShouldBeTrue(frame.ScreenText);
        var (x, y) = CellOf(frame, "emails", "7");
        Background(frame, x, y).ShouldNotBe(Color.Default);
        var (ox, oy) = CellOf(frame, "orders", "12");
        Background(frame, ox, oy).ShouldBe(Color.Default);

        await terminal.WaitUntilAsync(f => Background(f, x, y) == Color.Default, AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task FailedPoll_KeepsTheListAndShowsNoError_ThenRecovers()
    {
        var app = WithQueues();
        app.LiveCounts.Interval = TimeSpan.FromMilliseconds(50);
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);
        var loads = app.Queues.Loads;

        app.Queues.LoadError = new InvalidOperationException("boom");
        await terminal.WaitUntilAsync(_ => app.Queues.Loads >= loads + 3, AppHarness.Timeout, cancellationToken: Ct);
        var frame = terminal.CurrentFrame;
        frame.ContainsText("boom").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("orders").ShouldBeTrue(frame.ScreenText);

        app.Queues.Update("orders", q => q with { ActiveMessageCount = 13 });
        app.Queues.LoadError = null;
        await terminal.WaitUntilAsync(f => f.GetLine(RowOf(f, "orders")).Contains(" 13 "), AppHarness.Timeout, cancellationToken: Ct);
    }

    private static Color Background(TerminalFrame frame, int x, int y) => frame[x, y].Style?.Background ?? Color.Default;

    // The count in a row; counts are right-aligned, so match it with the spaces around it.
    private static (int X, int Y) CellOf(TerminalFrame frame, string row, string count)
    {
        var y = RowOf(frame, row);
        return (frame.GetLine(y).IndexOf($" {count} ", StringComparison.Ordinal) + 1, y);
    }

    [Test]
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

    [Test]
    public async Task TypingFiltersTheList()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await terminal.TypeAsync("pay", Ct);

        var frame = await terminal.WaitForTextAsync("Queues (1 of 3)", AppHarness.Timeout, Ct);
        frame.GetLine(RowOf(frame, "Queue ")).ShouldContain("🔍 pay");
        frame.ContainsText("payments").ShouldBeTrue();
        frame.ContainsText("orders").ShouldBeFalse();
    }

    [Test]
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

    [Test]
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

    [Test]
    public async Task LoadFailure_ShowsRetryHint()
    {
        var app = new AppHarness();
        app.Queues.LoadError = new InvalidOperationException("boom");
        await using var terminal = await app.OpenQueuesAsync();

        await terminal.WaitForTextAsync("Failed to load. Press [F5] to retry.", AppHarness.Timeout, Ct);
    }

    [Test]
    public async Task EnterOpensSelectedQueue()
    {
        await using var terminal = await WithQueues().OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (3)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Queues (") && f.ContainsText("payments"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
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

    [Test]
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

    [Test]
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

    [Test]
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

}
