using System.Text;
using Microsoft.Extensions.DependencyInjection;
using RazorConsole.Testing;

namespace ServiceBusConsole.Tests;

[NotInParallel(nameof(SessionContextTests))]
public sealed class SessionContextTests
{
    private static readonly MessageSource OrdersDlq = new MessageSource.Queue("orders", IsDlq: true);

    private static MessageInfo Message(long sequence, string subject) => new()
    {
        SequenceNumber = sequence,
        State = MessageState.Peeked,
        MessageId = $"msg-{sequence}",
        Subject = subject,
        Body = Encoding.UTF8.GetBytes($"{{\"id\":{sequence}}}"),
        EnqueuedTime = DateTimeOffset.UnixEpoch,
    };

    private static async Task<(AppHarness App, HeadlessTerminal Terminal, SessionContext Session)> OpenOrdersAsync()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 2 });
        app.Messages.Messages.AddRange([Message(1, "order-created"), Message(2, "order-paid")]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitUntilAsync(f => f.ContainsText("order-created") && f.ContainsText("order-paid"), AppHarness.Timeout, cancellationToken: Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        return (app, terminal, terminal.Services.GetRequiredService<SessionContext>());
    }

    [Test]
    public async Task PeekingAndOpeningAMessage_IsInTheSnapshot()
    {
        var (_, terminal, session) = await OpenOrdersAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.DownArrow, '\0', cancellationToken: Ct);
        await terminal.WaitUntilAsync(_ => session.Snapshot().Message?.SequenceNumber == 2, AppHarness.Timeout, cancellationToken: Ct);
        var inList = session.Snapshot();
        inList.Namespace.ShouldBe(NamespaceInfo.Emulator);
        inList.Subscription.ShouldBe(AzureSubscription.Emulator);
        inList.Source.ShouldBe(OrdersDlq);
        inList.Mode.ShouldBe("Peek");

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Message #2", AppHarness.Timeout, Ct);

        var snapshot = session.Snapshot();
        snapshot.Message?.SequenceNumber.ShouldBe(2);
        snapshot.Source.ShouldBe(OrdersDlq);
        snapshot.Mode.ShouldBe("Peek");
        snapshot.Activity.Select(a => (a.Kind, a.Text, a.Source)).ShouldBe(
        [
            (ActivityKind.Navigated, "Connected to namespace localhost", null),
            (ActivityKind.Navigated, "Opened /queues", null),
            (ActivityKind.Navigated, "Opened /queues/orders", null),
            (ActivityKind.Loaded, "Peeked 2 message(s)", OrdersDlq),
            (ActivityKind.Navigated, "Opened message #2", OrdersDlq),
        ]);
        var markdown = snapshot.ToMarkdown();
        markdown.ShouldContain("- Queue: `orders`, dead-letter queue\n");
        markdown.ShouldContain("## Message #2\n");
        markdown.ShouldContain("\"id\": 2");
    }

    [Test]
    public async Task BackOnTheQueueList_NoMessageIsInView()
    {
        var (_, terminal, session) = await OpenOrdersAsync();
        await using var _ = terminal;

        await terminal.SendKeyAsync(ConsoleKey.Escape, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Queues (1)", AppHarness.Timeout, Ct);

        var snapshot = session.Snapshot();
        snapshot.Namespace.ShouldBe(NamespaceInfo.Emulator);
        snapshot.Source.ShouldBeNull();
        snapshot.Message.ShouldBeNull();
        snapshot.Activity[^1].Text.ShouldBe("Opened /queues");
    }

    [Test]
    public async Task APurge_IsRecordedWithItsResult()
    {
        var (_, terminal, session) = await OpenOrdersAsync();
        await using var _ = terminal;

        var table = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.F11, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Type yes to confirm", AppHarness.Timeout, Ct);
        await AppHarness.WaitForFocusAsync(terminal, "0", table);
        await terminal.TypeAsync("yes\r", Ct);

        await terminal.WaitUntilAsync(_ => session.Activity.Entries[^1].Kind == ActivityKind.Operation, AppHarness.Timeout, cancellationToken: Ct);
        session.Activity.Entries[^1].ShouldBe(new ActivityEntry(
            session.Activity.Entries[^1].At, ActivityKind.Operation, "Purging orders DLQ: Purged 2 message(s).", OrdersDlq));
    }

    [Test]
    public async Task AFailedLoad_IsRecordedAsAnError()
    {
        var (app, terminal, session) = await OpenOrdersAsync();
        await using var _ = terminal;
        app.Messages.PeekError = new InvalidOperationException("Namespace unreachable");

        await terminal.SendKeyAsync(ConsoleKey.F6, '\0', cancellationToken: Ct);
        await terminal.WaitForTextAsync("Namespace unreachable", AppHarness.Timeout, Ct);

        await terminal.WaitUntilAsync(_ => session.Activity.Entries[^1].Kind == ActivityKind.Error, AppHarness.Timeout, cancellationToken: Ct);
        session.Activity.Entries[^1].Text.ShouldBe("Namespace unreachable");
    }
}
