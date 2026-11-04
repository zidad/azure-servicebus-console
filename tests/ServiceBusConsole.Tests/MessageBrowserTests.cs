using System.Text;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(MessageBrowserTests))]
public sealed class MessageBrowserTests
{
    private static MessageInfo Message(long sequence, string subject, string body) => new()
    {
        SequenceNumber = sequence,
        State = MessageState.Peeked,
        MessageId = $"msg-{sequence}",
        Subject = subject,
        Body = Encoding.UTF8.GetBytes(body),
        EnqueuedTime = DateTimeOffset.UnixEpoch,
    };

    private static async Task<(AppHarness App, RazorConsole.Testing.HeadlessTerminal Terminal)> OpenOrdersAsync()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", ActiveMessageCount = 2 });
        app.Messages.Messages.AddRange([Message(1, "order-created", "{\"id\":1}"), Message(2, "order-paid", "{\"id\":2}")]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("order-created", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        return (app, terminal);
    }

    [Test]
    public async Task OpeningQueue_PeeksItsDeadLetterQueue()
    {
        var (app, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("order-created") && f.ContainsText("order-paid"), AppHarness.Timeout, cancellationToken: Ct);

        frame.ContainsText("1  📬 Active").ShouldBeTrue(frame.ScreenText);
        frame.GetLine(0).ShouldContain("🧪 Local Emulator  ›  🌐 localhost  ›  📬 Queues  ›  orders 💀 DLQ");
        app.Messages.Peeked.ShouldBe([new MessageSource.Queue("orders", IsDlq: true)]);
    }

    [Test]
    public async Task EnterOnMessage_ShowsItsBody()
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        await terminal.WaitForTextAsync("order-created", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        frame.ContainsText("\"id\": 1").ShouldBeTrue(frame.ScreenText);
        frame.GetLine(0).ShouldContain("🧪 Local Emulator  ›  🌐 localhost  ›  📬 orders DLQ  ›  📨 Message #1");
    }

    [Test]
    public async Task ActingOnOneMessageFromItsDetailPage_KeepsTheOtherSelections()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", ActiveMessageCount = 3 });
        app.Messages.Messages.AddRange([Message(1, "first", "{}"), Message(2, "second", "{}"), Message(3, "third", "{}")]);
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("first", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        await terminal.SendKeyAsync(ConsoleKey.F6, '\0', cancellationToken: Ct);
        // The button flips before the active queue is peeked, and the list re-renders as it loads: wait until it stops.
        await AppHarness.WaitForSettledAsync(terminal, f => f.ContainsText("2  💀 DLQ") && f.ContainsText("3 message(s)"));
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(ConsoleKey.Spacebar, ' ', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Spacebar, ' ', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        var browserFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Message #3", AppHarness.Timeout, Ct);
        await WaitForDetailFocusAsync(terminal, browserFocus);
        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Send a copy", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await ClickOkAsync(terminal, "Sent 1 of 1.");
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        app.Messages.Sent.Select(s => s.SequenceNumber).ShouldBe([3L]);
    }

    /// <param name="azure">An Azure Standard namespace, with transactions, instead of the emulator.</param>
    private static async Task<(AppHarness App, RazorConsole.Testing.HeadlessTerminal Terminal)> PeekDlqAndSelectFirstAndThirdAsync(
        Action<AppHarness>? arrange = null, bool azure = false)
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 3 });
        app.Messages.Messages.AddRange([Message(1, "first", "{}"), Message(2, "second", "{}"), Message(3, "third", "{}")]);
        arrange?.Invoke(app);
        var terminal = azure ? await app.OpenAzureAsync(ConsoleKey.F1, height: 30) : await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("first", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);

        await terminal.SendKeyAsync(ConsoleKey.Spacebar, ' ', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Spacebar, ' ', cancellationToken: Ct);
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        return (app, terminal);
    }

    [Test]
    public async Task RetryFromPeek_LocksSendsInOrderAndCompletesTheSelection()
    {
        var (app, terminal) = await PeekDlqAndSelectFirstAndThirdAsync();
        await using var _ = terminal;
        terminal.CurrentFrame.ContainsText("R  🔁 Retry").ShouldBeTrue(terminal.CurrentFrame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("2 messages → PeekLock → Requeue to orders → Mark complete", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        var frame = await terminal.WaitUntilAsync(f => !f.ContainsText("first") && !f.ContainsText("third") && f.ContainsText("second"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("selected").ShouldBeFalse(frame.ScreenText);
        app.Messages.Sent.ShouldBe([(new MessageSource.Queue("orders", IsDlq: true), 1L), (new MessageSource.Queue("orders", IsDlq: true), 3L)]);
        app.Messages.Completed.ShouldBe([1L, 3L], ignoreOrder: true);
        app.Services.Transactions.Moved.ShouldBeEmpty();
    }

    [Test]
    public async Task RetryWithTransactions_ConfirmsTheMove_AndMovesThroughThem()
    {
        var (app, terminal) = await PeekDlqAndSelectFirstAndThirdAsync(azure: true);
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        var frame = await terminal.WaitForTextAsync("2 messages → PeekLock → Move to orders", AppHarness.Timeout, Ct);
        frame.ContainsText("move in one transaction").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Mark complete").ShouldBeFalse(frame.ScreenText);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Retried 2 of 2.", AppHarness.Timeout, Ct);
        app.Services.Transactions.Moved.ShouldBe([[1L, 3L]]);
        app.Messages.Sent.ShouldBeEmpty();
    }

    [Test]
    public async Task RetryFromPeek_MessageLockedElsewhere_StaysSelectedAndIsNotSent()
    {
        var (app, terminal) = await PeekDlqAndSelectFirstAndThirdAsync(a => a.Messages.LockedElsewhere.Add(3));
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retry 2 messages?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Retried 1 of 2", AppHarness.Timeout, Ct);
        frame.ContainsText("#3 not found in the dead-letter queue").ShouldBeTrue(frame.ScreenText);
        app.Messages.Sent.Select(s => s.SequenceNumber).ShouldBe([1L]);
        app.Messages.Completed.ShouldBe([1L]);

        await ClickOkAsync(terminal, "Retried 1 of 2");
        frame = await terminal.WaitForTextAsync("1 selected", AppHarness.Timeout, Ct);
        frame.ContainsText("third").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Retrying").ShouldBeFalse(frame.ScreenText);
    }

    // ColorCode's own JSON grammar backtracks exponentially on this (arrays of long strings) and hangs the render thread.
    [Test]
    public async Task Preview_HighlightsJsonWithAnArrayOfLongStrings()
    {
        var item = new string('x', 60);
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 1 });
        app.Messages.Messages.Add(Message(1, "scopes", $$"""{"scopes": ["{{item}}", "{{item}}", "{{item}}"], "done": true}"""));
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("\"done\": true", AppHarness.Timeout, Ct);
        frame.ContainsText(item[..30]).ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task DeadLetteredMessage_ShowsItsReasonAndDescriptionAboveTheProperties()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 1 });
        app.Messages.Messages.Add(Message(1, "order-created", "{\"id\":1}") with
        {
            DeadLetterReason = "MaxDeliveryCountExceeded",
            DeadLetterErrorDescription = "Message could not be consumed after 10 delivery attempts.",
        });
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Message could not be consumed", AppHarness.Timeout, Ct);
        var lines = Enumerable.Range(0, frame.Height).Select(frame.GetLine).ToList();
        var reason = lines.FindIndex(l => l.Contains("💀 MaxDeliveryCountExceeded Message could not be consumed after 10 delivery attempts."));
        reason.ShouldBeGreaterThan(0, frame.ScreenText);
        lines.FindIndex(l => l.Contains("📋 MESSAGE")).ShouldBeGreaterThan(reason);
        frame.ContainsText("Dead-letter reason").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task Zoom_StepsThroughTheList_SwitchesTabs_AndCopiesTheBody()
    {
        var (app, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        var list = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        var frame = await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        frame.ContainsText(" 1 📄 Body   2 📋 Properties   3 Application   4 Raw ").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("[ 1 of 2 ]").ShouldBeTrue(frame.ScreenText);
        await AppHarness.WaitForFocusAsync(terminal, "0", list);

        await terminal.SendKeyAsync(ConsoleKey.Oem6, ']', cancellationToken: Ct);
        frame = await terminal.WaitForTextAsync("Message #2", AppHarness.Timeout, Ct);
        frame.ContainsText("\"id\": 2").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("[ 2 of 2 ]").ShouldBeTrue(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.D2, '2', cancellationToken: Ct);
        frame = await terminal.WaitForTextAsync("order-paid", AppHarness.Timeout, Ct);
        frame.ContainsText("Sequence #").ShouldBeTrue(frame.ScreenText);

        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Copied the body of #2", AppHarness.Timeout, Ct);
        app.Copied.ShouldBe([$"\u001b]52;c;{Convert.ToBase64String(Encoding.UTF8.GetBytes("{\"id\":2}"))}\u0007"]);
    }

    [Test]
    public async Task Preview_FollowsTheCursor()
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;

        await terminal.WaitForTextAsync("\"id\": 1", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("\"id\": 2", AppHarness.Timeout, Ct);
        frame.ContainsText("\"id\": 1").ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("📨 #2 · 2 of 2 · ⏎ zoom").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("📋 MESSAGE").ShouldBeTrue(frame.ScreenText);
    }

    [Test]
    public async Task TabMovesIntoTheBody_WherePageKeysStillWork()
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        var listFocus = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != listFocus, AppHarness.Timeout, cancellationToken: Ct);
        AppHarness.FocusedAttribute(terminal, "data-scrollbar").ShouldBe("true");
        AppHarness.FocusedAttribute(terminal, "data-focus-order").ShouldBe("2");
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
    }

    [Test]
    public async Task HeaderCheckbox_SelectsAll_ThenClears()
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        var frame = await terminal.WaitForTextAsync("order-paid", AppHarness.Timeout, Ct);
        var header = Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains("Message ID"));
        var x = frame.GetLine(header).IndexOf(Icons.Unselected, StringComparison.Ordinal);

        await terminal.ClickAsync(x, header, Ct);
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);

        await terminal.ClickAsync(x, header, Ct);
        await terminal.WaitUntilAsync(f => !f.ContainsText("selected"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task Purge_ReturnsToTheList()
    {
        var (app, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.F11, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Purge ALL messages", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await ClickOkAsync(terminal, "Purged 2 message(s).");
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        app.Messages.Purged.ShouldBe([new MessageSource.Queue("orders", IsDlq: true)]);
    }

    [Test]
    public async Task RetryingEveryMessage_ReturnsToTheList()
    {
        var (app, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;

        var tableFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.A, '\u0001', ConsoleModifiers.Control, Ct);
        await terminal.WaitForTextAsync("2 selected", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retry 2 messages?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        // The result stays up until OK; only then back to the list.
        var frame = await terminal.WaitForTextAsync("Retried 2 of 2.", AppHarness.Timeout, Ct);
        frame.ContainsText("[Enter] OK").ShouldBeTrue(frame.ScreenText);
        frame.ContainsText("Queues (1)").ShouldBeFalse(frame.ScreenText);
        app.Messages.Completed.Order().ShouldBe([1L, 2L]);

        // The list is empty: the focus order 1 that is not the table is the dialog's OK.
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != tableFocus && AppHarness.FocusedAttribute(terminal, "data-focus-order") == "1",
            AppHarness.Timeout, cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
    }

    [Test]
    public async Task RetryingTheLastMessageFromItsDetailPage_ReturnsToTheList()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 1 });
        app.Messages.Messages.Add(Message(1, "only", "{}"));
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("only", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);

        var browserFocus = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        await WaitForDetailFocusAsync(terminal, browserFocus);
        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retry message #1?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        await ClickOkAsync(terminal, "Retried 1 of 1.");
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        app.Messages.Completed.ShouldBe([1L]);
    }

    [Test]
    public async Task Retry_ShowsAProgressDialogWithItsSteps_ThenItsResultUntilOk()
    {
        var hold = new TaskCompletionSource();
        var (app, terminal) = await PeekDlqAndSelectFirstAndThirdAsync(a => a.Messages.HoldSend = hold);
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retry 2 messages?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);

        // The send is held, so the dialog settles with the lock done and the send running.
        var frame = await terminal.WaitUntilAsync(
            f => f.ContainsText("Retrying 2 messages") && f.ContainsText($"{Icons.Running} Requeue to orders") && f.ContainsText($"{Icons.Done} PeekLock"),
            AppHarness.Timeout, cancellationToken: Ct);
        Line(frame, "PeekLock").ShouldContain($"{Icons.Done} PeekLock");
        Line(frame, "PeekLock").ShouldContain("2/2");
        Line(frame, "Requeue to orders").ShouldContain($"{Icons.Running} Requeue to orders");
        Line(frame, "Requeue to orders").ShouldContain("0/2");
        Line(frame, "Mark complete").ShouldContain($"{Icons.Pending} Mark complete");

        hold.SetResult();
        frame = await terminal.WaitForTextAsync("Retried 2 of 2.", AppHarness.Timeout, Ct);
        Line(frame, "Mark complete").ShouldContain($"{Icons.Done} Mark complete");
        Line(frame, "Mark complete").ShouldContain("2/2");
        app.Messages.Completed.Order().ShouldBe([1L, 3L]);

        await ClickOkAsync(terminal, "Retried 2 of 2.");
        await terminal.WaitUntilAsync(f => !f.ContainsText("Retrying 2 messages") && f.ContainsText("second"), AppHarness.Timeout, cancellationToken: Ct);
    }

    private static async Task ClickOkAsync(RazorConsole.Testing.HeadlessTerminal terminal, string result)
    {
        var frame = await terminal.WaitUntilAsync(f => f.ContainsText(result) && f.ContainsText("[Enter] OK"), AppHarness.Timeout, cancellationToken: Ct);
        var y = Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains("[Enter] OK"));
        await terminal.ClickAsync(frame.GetLine(y).IndexOf("[Enter] OK", StringComparison.Ordinal) + 2, y, Ct);
    }

    private static string Line(RazorConsole.Testing.TerminalFrame frame, string text)
        => frame.GetLine(Enumerable.Range(0, frame.Height).First(y => frame.GetLine(y).Contains(text)));

    // The detail page focuses its body after rendering; keys sent before that reach the old page.
    private static Task WaitForDetailFocusAsync(RazorConsole.Testing.HeadlessTerminal terminal, string? browserFocus)
        => terminal.WaitUntilAsync(_ => terminal.FocusKey != browserFocus && AppHarness.FocusedAttribute(terminal, "data-scrollbar") == "true",
            AppHarness.Timeout, cancellationToken: Ct);

    [Test]
    public async Task Peek_FillsTheListPageByPage_AndEscapeStopsIt()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 250 });
        app.Messages.Messages.AddRange(Enumerable.Range(1, 250).Select(i => Message(i, $"subject-{i}", "{}")));
        app.Messages.PausePeek = new TaskCompletionSource();
        await using var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        // Default peek is 100: one page arrives; ask for 250 so the second page waits.
        await terminal.WaitForTextAsync("subject-1 ", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.F7, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Peek messages", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, focusOrder: "100"); // the dialog's count input
        await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Backspace, '\b', cancellationToken: Ct);
        await terminal.TypeAsync("250", Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("so far · Esc cancels", AppHarness.Timeout, Ct);
        frame.ContainsText("100 message(s)").ShouldBeTrue(frame.ScreenText);
        await AppHarness.WaitForTableFocusAsync(terminal);
        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        frame = await terminal.WaitUntilAsync(f => !f.ContainsText("Esc cancels") && f.ContainsText("100 message(s)"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Queues (1)").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    [Arguments(0)] // count field
    [Arguments(1)] // from-sequence field
    [Arguments(3)] // Cancel button
    public async Task EscapeInPeekDialog_ClosesIt(int tabs)
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        await terminal.SendKeyAsync(ConsoleKey.F7, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Peek messages", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, focusOrder: "100");
        for (var i = 0; i < tabs; i++)
        {
            var previous = terminal.FocusKey;
            await terminal.SendKeyAsync(ConsoleKey.Tab, '\t', cancellationToken: Ct);
            await terminal.WaitUntilAsync(f => terminal.FocusKey != previous, AppHarness.Timeout, cancellationToken: Ct);
        }

        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        await terminal.WaitUntilAsync(f => !f.ContainsText("Peek messages"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
    }

    [Test]
    public async Task EscapeCancelsARunningRetry()
    {
        var (app, terminal) = await PeekDlqAndSelectFirstAndThirdAsync(a => a.Messages.HoldSend = new TaskCompletionSource());
        await using var _ = terminal;
        var tableFocus = terminal.FocusKey;

        await terminal.SendKeyAsync(ConsoleKey.F10, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Retry 2 messages?", AppHarness.Timeout, Ct);
        await terminal.SendKeyAsync(ConsoleKey.Y, 'y', cancellationToken: Ct);
        await terminal.WaitForTextAsync("[Esc] Cancel", AppHarness.Timeout, Ct);
        // Escape reaches the browser through the table; it gets focus back once the confirm modal is gone.
        await AppHarness.WaitForTableFocusAsync(terminal);

        await terminal.SendKeyAsync(ConsoleKey.Escape, '\u001b', cancellationToken: Ct);

        await terminal.WaitForTextAsync("Cancelled. Retried 0 of 2", AppHarness.Timeout, Ct);
        app.Messages.Completed.ShouldBeEmpty();

        // Both messages are left, so OK stays on the browser. Wait for the OK button to take focus from the table.
        await terminal.WaitUntilAsync(_ => terminal.FocusKey is { } key && key != tableFocus, AppHarness.Timeout, cancellationToken: Ct);
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        var frame = await terminal.WaitUntilAsync(f => !f.ContainsText("Retrying 2 messages") && f.ContainsText("2 selected"), AppHarness.Timeout, cancellationToken: Ct);
        frame.ContainsText("Queues (1)").ShouldBeFalse(frame.ScreenText);
    }
}
