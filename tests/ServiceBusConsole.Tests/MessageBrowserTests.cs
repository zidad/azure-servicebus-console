using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.Tests;

public sealed class MessageBrowserTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    private static MessageInfo Message(long sequence, string subject, string body) => new(
        ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString(body),
            messageId: $"msg-{sequence}",
            subject: subject,
            sequenceNumber: sequence,
            enqueuedTime: DateTimeOffset.UnixEpoch),
        MessageState.Peeked);

    private static async Task<(AppHarness App, RazorConsole.Testing.HeadlessTerminal Terminal)> OpenOrdersAsync()
    {
        var app = new AppHarness();
        app.Queues.Queues.Add(new QueueInfo { Name = "orders", ActiveMessageCount = 2 });
        app.Messages.Messages.AddRange([Message(1, "order-created", "{\"id\":1}"), Message(2, "order-paid", "{\"id\":2}")]);
        var terminal = await app.OpenQueuesAsync();
        await terminal.WaitForTextAsync("1 queue(s)", AppHarness.Timeout, Ct);
        var queueTable = terminal.FocusKey;
        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);
        await terminal.WaitForTextAsync("order-created", AppHarness.Timeout, Ct);
        await AppHarness.WaitForTableFocusAsync(terminal, queueTable);
        return (app, terminal);
    }

    [Fact]
    public async Task OpeningQueue_PeeksItsDeadLetterQueue()
    {
        var (app, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;

        var frame = await terminal.WaitUntilAsync(f => f.ContainsText("order-created") && f.ContainsText("order-paid"), AppHarness.Timeout, cancellationToken: Ct);

        frame.ContainsText("[F6] View Active").ShouldBeTrue(frame.ScreenText);
        app.Messages.Peeked.ShouldBe([new MessageSource("orders", IsDlq: true)]);
    }

    [Fact]
    public async Task EnterOnMessage_ShowsItsBody()
    {
        var (_, terminal) = await OpenOrdersAsync();
        await using var _ = terminal;
        await terminal.WaitForTextAsync("order-created", AppHarness.Timeout, Ct);

        await terminal.SendKeyAsync(ConsoleKey.Enter, '\r', cancellationToken: Ct);

        var frame = await terminal.WaitForTextAsync("Message #1", AppHarness.Timeout, Ct);
        frame.ContainsText("\"id\": 1").ShouldBeTrue(frame.ScreenText);
    }
}
