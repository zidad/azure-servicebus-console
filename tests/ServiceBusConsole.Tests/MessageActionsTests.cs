using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(MessageActionsTests))]
public sealed class MessageActionsTests
{
    private const string ActionsButton = ".  ⚡ Actions…";
    private static readonly MessageSource OrdersDlq = new MessageSource.Queue("orders", IsDlq: true);

    private static MessageInfo Message(long sequence, string subject) => new()
    {
        SequenceNumber = sequence,
        State = MessageState.Peeked,
        MessageId = $"msg-{sequence}",
        Subject = subject,
        Body = Encoding.UTF8.GetBytes("{}"),
        EnqueuedTime = DateTimeOffset.UnixEpoch,
    };

    private static FakeMessageAction Returning(string label, ActionResult result) => new("fake", label, (_, _) => Task.FromResult(result));

    private static async Task<HeadlessTerminal> OpenOrdersAsync(AppHarness app)
    {
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 2 });
        app.Messages.Messages.AddRange([Message(1, "order-created"), Message(2, "order-paid")]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => f.ContainsText("order-created") && f.ContainsText("order-paid"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        return terminal;
    }

    private static async Task OpenPickerAsync(HeadlessTerminal terminal)
    {
        await terminal.SendKeyAsync(ConsoleKey.F2, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Actions on message", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "300"); // MessageActionsDialog.PickerFocusOrder
    }

    private static IReadOnlyList<ActivityEntry> Activity(HeadlessTerminal terminal) => terminal.Services.GetRequiredService<ActivityLog>().Entries;

    private static Task Press(HeadlessTerminal terminal, ConsoleKey key, char c = '\0') => terminal.SendKeyAsync(key, c, cancellationToken: Ct);

    [Test]
    public async Task F2InTheBrowser_PicksAnAction_ShowsProgressThenTheResult_AndRecordsIt()
    {
        var release = new TaskCompletionSource();
        var app = new AppHarness();
        var create = new FakeMessageAction("fake", "Create issue", async (context, ct) =>
        {
            context.Progress.Report("Creating issue in acme/orders…");
            await release.Task.WaitAsync(ct);
            return new ActionResult("Created issue #12", new Uri("https://github.com/acme/orders/issues/12"));
        });
        app.Plugins.Add(new FakePlugin { Actions = [Returning("Copy", new ActionResult("Copied")), create] });
        await using var terminal = await OpenOrdersAsync(app);
        await Press(terminal, ConsoleKey.DownArrow);
        terminal.CurrentFrame.ContainsText(ActionsButton).ShouldBeTrue(terminal.CurrentFrame.ScreenText);

        await OpenPickerAsync(terminal);
        var picker = await terminal.WaitUntilAsync(f => f.ContainsText("Create issue"), AppHarness.Timeout, cancellationToken: Ct);
        picker.ContainsText("Actions on message #2").ShouldBeTrue(picker.ScreenText);
        picker.ContainsText("🐙 Copy").ShouldBeTrue(picker.ScreenText);
        picker.ContainsText("Fake").ShouldBeTrue(picker.ScreenText);
        await Press(terminal, ConsoleKey.DownArrow);
        await terminal.WaitUntilAsync(_ => AppHarness.FocusedAttribute(terminal, "data-focus-order") == "301", AppHarness.Timeout, cancellationToken: Ct);
        await Press(terminal, ConsoleKey.Enter, '\r');

        await terminal.WaitForTextAsync("Creating issue in acme/orders…", AppHarness.Timeout, Ct);
        release.SetResult();
        var result = await terminal.WaitUntilAsync(f => f.ContainsText("Created issue #12") && f.ContainsText("[O] 📂 Open"), AppHarness.Timeout, cancellationToken: Ct);
        result.ContainsText("https://github.com/acme/orders/issues/12").ShouldBeTrue(result.ScreenText);
        await AppHarness.WaitForFocusAsync(terminal, "500"); // ResultFocusOrder
        await Press(terminal, ConsoleKey.O, 'o');
        await terminal.WaitUntilAsync(_ => !app.PluginHost.Opened.IsEmpty, AppHarness.Timeout, cancellationToken: Ct);
        app.PluginHost.Opened.ShouldBe(["https://github.com/acme/orders/issues/12"]);

        var context = create.Contexts.ShouldHaveSingleItem();
        context.Message.SequenceNumber.ShouldBe(2);
        context.Source.ShouldBe(OrdersDlq);
        context.Session.Message?.SequenceNumber.ShouldBe(2);
        context.Settings["repo"].ShouldBe("zidad/example");
        context.Host.ShouldBeSameAs(app.PluginHost);
        Activity(terminal).ShouldContain(e => e.Kind == ActivityKind.Operation && e.Text == "Fake: Created issue #12" && e.Source == OrdersDlq);

        await Press(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitUntilAsync(f => !f.ContainsText("Created issue #12"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
    }

    [Test]
    public async Task F2OnTheDetailPage_RunsOnTheOpenMessage()
    {
        var app = new AppHarness();
        var copy = Returning("Copy", new ActionResult("Copied", FilePath: "/tmp/orders-dlq-1.json"));
        app.Plugins.Add(new FakePlugin { Actions = [copy] });
        await using var terminal = await OpenOrdersAsync(app);
        var browserFocus = terminal.FocusKey;
        await Press(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        await terminal.WaitUntilAsync(_ => terminal.FocusKey != browserFocus && AppHarness.FocusedAttribute(terminal, "data-scrollbar") == "true",
            AppHarness.Timeout, cancellationToken: Ct);
        terminal.CurrentFrame.ContainsText(ActionsButton).ShouldBeTrue(terminal.CurrentFrame.ScreenText);

        await OpenPickerAsync(terminal);
        await Press(terminal, ConsoleKey.Enter, '\r');

        var result = await terminal.WaitUntilAsync(f => f.ContainsText("Copied"), AppHarness.Timeout, cancellationToken: Ct);
        result.ContainsText("📄 /tmp/orders-dlq-1.json").ShouldBeTrue(result.ScreenText);
        var context = copy.Contexts.ShouldHaveSingleItem();
        context.Message.SequenceNumber.ShouldBe(1);
        context.Session.Message?.SequenceNumber.ShouldBe(1);
        context.Source.ShouldBe(OrdersDlq);
    }

    [Test]
    public async Task AFailingAction_ShowsTheError_AndRecordsIt()
    {
        var app = new AppHarness();
        app.Plugins.Add(new FakePlugin { Actions = [new FakeMessageAction("fake", "Explode", (_, _) => throw new InvalidOperationException("gh: not logged in"))] });
        await using var terminal = await OpenOrdersAsync(app);

        await OpenPickerAsync(terminal);
        await Press(terminal, ConsoleKey.Enter, '\r');

        await terminal.WaitForTextAsync("Fake: gh: not logged in", AppHarness.Timeout, Ct);
        await terminal.WaitUntilAsync(_ => Activity(terminal).Any(e => e.Kind == ActivityKind.Error), AppHarness.Timeout, cancellationToken: Ct);
        Activity(terminal).ShouldContain(e => e.Kind == ActivityKind.Error && e.Text == "Fake: gh: not logged in");
    }

    [Test]
    public async Task EscapeWhileRunning_CancelsTheAction()
    {
        var app = new AppHarness();
        var cancelled = new TaskCompletionSource();
        app.Plugins.Add(new FakePlugin
        {
            Actions =
            [
                new FakeMessageAction("fake", "Wait", async (context, ct) =>
                {
                    context.Progress.Report("Waiting…");
                    using var _ = ct.Register(() => cancelled.TrySetResult());
                    await Task.Delay(Timeout.Infinite, ct);
                    return new ActionResult("never");
                }),
            ],
        });
        await using var terminal = await OpenOrdersAsync(app);
        await OpenPickerAsync(terminal);
        await Press(terminal, ConsoleKey.Enter, '\r');
        await terminal.WaitForTextAsync("Waiting…", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "400"); // CancelFocusOrder

        await Press(terminal, ConsoleKey.Escape, '\u001b');

        await cancelled.Task.WaitAsync(AppHarness.Timeout, Ct);
        await terminal.WaitUntilAsync(f => !f.ContainsText("Waiting…"), AppHarness.Timeout, cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => Activity(terminal).Any(e => e.Text == "Fake: Wait cancelled"), AppHarness.Timeout, cancellationToken: Ct);
    }

    [Test]
    public async Task ADisabledPlugin_OffersNoActions()
    {
        var app = new AppHarness();
        app.Plugins.Add(new FakePlugin { Actions = [Returning("Copy", new ActionResult("Copied"))] });
        app.Settings["plugins:fake:enabled"] = "false";
        await using var terminal = await OpenOrdersAsync(app);

        await Press(terminal, ConsoleKey.F2);
        await Press(terminal, ConsoleKey.DownArrow);

        var session = terminal.Services.GetRequiredService<SessionContext>();
        await terminal.WaitUntilAsync(_ => session.Snapshot().Message?.SequenceNumber == 2, AppHarness.Timeout, cancellationToken: Ct);
        var frame = terminal.CurrentFrame;
        frame.ContainsText(ActionsButton).ShouldBeFalse(frame.ScreenText);
        frame.ContainsText("Actions on message").ShouldBeFalse(frame.ScreenText);
    }

    [Test]
    public async Task EscapeInThePicker_ClosesIt()
    {
        var app = new AppHarness();
        app.Plugins.Add(new FakePlugin { Actions = [Returning("Copy", new ActionResult("Copied"))] });
        await using var terminal = await OpenOrdersAsync(app);
        await OpenPickerAsync(terminal);

        await Press(terminal, ConsoleKey.Escape, '\u001b');

        await terminal.WaitUntilAsync(f => !f.ContainsText("Actions on message"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal);
        terminal.CurrentFrame.ContainsText("order-created").ShouldBeTrue(terminal.CurrentFrame.ScreenText);
    }
}
