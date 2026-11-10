using System.Text;
using RazorConsole.Testing;
using ServiceBusConsole.Components;
using Spectre.Console;

namespace ServiceBusConsole.Tests;

/// <summary>The message browser in Locked mode: messages received with PeekLock.</summary>
[NotInParallel(nameof(LockedBrowserTests))]
public sealed class LockedBrowserTests
{
    private static MessageInfo Message(long sequence, string subject) => new()
    {
        SequenceNumber = sequence,
        State = MessageState.Peeked,
        MessageId = $"msg-{sequence}",
        Subject = subject,
        Body = Encoding.UTF8.GetBytes("{}"),
        EnqueuedTime = DateTimeOffset.UnixEpoch,
    };

    /// <summary>Opens the active queue of orders and receives its three messages with PeekLock.</summary>
    private static async Task<(AppHarness App, HeadlessTerminal Terminal)> ReceiveOrdersAsync()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", ActiveMessageCount = 3 });
        app.Messages.Messages.AddRange([Message(1, "first"), Message(2, "second"), Message(3, "third")]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("first", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        await terminal.SendKeyAsync(ConsoleKey.D1, '1', cancellationToken: Ct);
        await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("2  💀 DLQ") && f.ContainsText("3 message(s)"));
        await AppHarness.WaitForTableFocusAsync(terminal);

        var table = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.R, 'r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Receive messages", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "100", table);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await AppHarness.WaitForSettledAsync(terminal, f => Counts(f, "3 received", 3));
        await AppHarness.WaitForTableFocusAsync(terminal);
        return (app, terminal);
    }

    private static async Task ClickOkAsync(HeadlessTerminal terminal, string result)
    {
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText(result) && f.ContainsText("⏎  OK"), AppHarness.Timeout, cancellationToken: Ct);
        var y = Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains("⏎  OK"));
        await terminal.ClickAsync(frame.GetLine(y).IndexOf("⏎  OK", StringComparison.Ordinal) + 2, y, Ct);
        await terminal.WaitUntilAsync(f => !f.ContainsText(result), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
    }

    /// <summary>The counts beside the tabs, and the strip's held count.</summary>
    private static bool Counts(TerminalFrame frame, string counts, int held) =>
        frame.GetLine(2).TrimEnd().EndsWith(counts, StringComparison.Ordinal) && frame.ContainsText($"🔒 LOCKED  {held} held · locks renew automatically");

    private static Task<HeadlessTerminalFrame> WaitForCountsAsync(HeadlessTerminal terminal, string counts, int held) =>
        terminal.WaitUntilAsync(f => Counts(f, counts, held), AppHarness.Timeout, cancellationToken: Ct);

    private static int Row(TerminalFrame frame, string text) => Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains(text));

    private static Color Foreground(TerminalFrame frame, string rowText, string cellText)
    {
        var y = Row(frame, rowText);
        return frame[frame.GetLine(y).IndexOf(cellText, StringComparison.Ordinal), y].Style?.Foreground ?? Color.Default;
    }

    [Test]
    public async Task ReceivedMessages_ShowTheirStateAndLock()
    {
        var (_, terminal) = await ReceiveOrdersAsync();
        await using var _ = terminal;

        var frame = terminal.CurrentFrame;
        frame.ContainsText("State").ShouldBeTrue(frame.ScreenText);
        frame.GetLine(Row(frame, "third")).ShouldContain("🔒 held");
        frame.GetLine(Row(frame, "third")).ShouldContain("🔄 auto-renew");
    }

    [Test]
    public async Task Completing_KeepsTheRowDimmed_AndItCannotBeSettledAgain()
    {
        var (app, terminal) = await ReceiveOrdersAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.C, 'c', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Complete message #1?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await ClickOkAsync(terminal, "Completed 1 of 1.");

        var frame = await WaitForCountsAsync(terminal, "3 received · 1 completed", 2);
        frame.GetLine(Row(frame, "first")).ShouldContain("✅ completed");
        frame.GetLine(Row(frame, "first")).ShouldNotContain("auto-renew");
        frame.ContainsText("✅ Completed (removed)").ShouldBeTrue(frame.ScreenText);
        app.Messages.Completed.ShouldBe([1L]);

        // Off the cursor, the settled row's text is dimmed; a held row's is not.
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        frame = await AppHarness.WaitForSettledAsync(terminal, f => f.GetLine(Row(f, "second")).Contains(RowCheck.Empty));
        Foreground(frame, "first", "msg-1").ShouldBe(Theme.Settled);
        Foreground(frame, "third", "msg-3").ShouldBe(Color.Default);

        // Back on it, complete does nothing, and select all leaves it out.
        await terminal.SendKeyAsync(ConsoleKey.UpArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.C, 'c', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.A, '\u0001', ConsoleModifiers.Control, Ct);
        frame = await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        frame.ContainsText("Complete message #1?").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("Complete 2").ShouldBeTrue(frame.ScreenText);
        app.Messages.Completed.ShouldBe([1L]);
    }

    [Test]
    public async Task AbandonDeadLetterAndLostLock_EachShowTheirState()
    {
        var (app, terminal) = await ReceiveOrdersAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.A, 'a', cancellationToken: Ct);
        await ClickOkAsync(terminal, "Abandoned 1 of 1.");
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.X, 'X', ConsoleModifiers.Shift, Ct);
        await terminal.WaitForTextAsync("Move message #2 to the dead-letter queue?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await ClickOkAsync(terminal, "Dead-lettered 1 of 1.");
        app.Messages.Received.ShouldNotBeNull().LoseLock(3);

        var frame = await WaitForCountsAsync(terminal, "3 received · 1 abandoned · 1 dead-lettered", 0);
        frame.GetLine(Row(frame, "first")).ShouldContain("🔓 abandoned");
        frame.GetLine(Row(frame, "second")).ShouldContain("💀 dead-lettered");
        frame.GetLine(Row(frame, "third")).ShouldContain("❌ lock lost");
        app.Messages.Abandoned.ShouldBe([1L]);
        app.Messages.DeadLettered.ShouldBe([2L]);
    }

    [Test]
    public async Task DetailPage_StepsOverSettledMessages_AndOffersSettlingOnlyForHeldOnes()
    {
        var (app, terminal) = await ReceiveOrdersAsync();
        await using var _ = terminal;
        await terminal.SendKeyAsync(ConsoleKey.A, 'a', cancellationToken: Ct);
        await ClickOkAsync(terminal, "Abandoned 1 of 1.");

        var browserFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        var frame = await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != browserFocus && AppHarness.FocusedAttribute(terminal, "data-scrollbar") == "true",
            AppHarness.Timeout, cancellationToken: Ct);
        frame = await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("1 of 3"));
        frame.ContainsText("Complete").ShouldBeFalse(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.Oem6, ']', cancellationToken: Ct);
        frame = await terminal.WaitUntilAsync(f => f.ContainsText("Message #2") && f.ContainsText("2 of 3"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Complete").ShouldBeTrue(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.C, 'c', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Complete message #2?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await ClickOkAsync(terminal, "Completed 1 of 1.");

        frame = await WaitForCountsAsync(terminal, "3 received · 1 completed · 1 abandoned", 1);
        frame.GetLine(Row(frame, "second")).ShouldContain("✅ completed");
        app.Messages.Completed.ShouldBe([2L]);
    }
}
