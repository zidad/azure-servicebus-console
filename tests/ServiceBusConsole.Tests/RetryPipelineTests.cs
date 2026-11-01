namespace ServiceBusConsole.Tests;

public sealed class RetryPipelineTests
{
    [Test]
    public async Task Retry_CompletesTheFirstChunk_WhileTheScanIsStillRunning()
    {
        var service = new FakeMessageService { PauseScan = new TaskCompletionSource(), PauseScanAfter = 100 };
        service.Messages.AddRange(Enumerable.Range(1, 250).Select(i => Message(i)));
        var browser = new MessageBrowser(new MessageSource.Queue("orders", IsDlq: true), new FakeEmulatorNamespace(new FakeServices { Messages = service }));
        await browser.PeekAsync(250, null);
        browser.SelectAll();

        var retry = browser.RetryAsync(browser.Targets);

        // The scan is paused after 100 messages, yet those are already sent and completed.
        await WaitUntilAsync(() => service.Completed.Count == 100);
        retry.IsCompleted.ShouldBeFalse();
        service.PauseScan.SetResult();
        await retry.WaitAsync(TimeSpan.FromSeconds(10), Ct);

        service.Completed.Count.ShouldBe(250);
        service.Sent.Select(s => s.SequenceNumber).ShouldBe(Enumerable.Range(1, 250).Select(i => (long)i));
        browser.Messages.ShouldBeEmpty();
        var op = browser.Operation.ShouldNotBeNull();
        op.Result.ShouldBe("Retried 250 of 250.");
        op.Outcome.ShouldBe(OperationOutcome.Succeeded);
    }

    [Test]
    public async Task CancellingRetry_CompletesWhatWasSent_AndKeepsTheRestSelected()
    {
        var service = new FakeMessageService { PauseScan = new TaskCompletionSource(), PauseScanAfter = 100 };
        service.Messages.AddRange(Enumerable.Range(1, 250).Select(i => Message(i)));
        var browser = new MessageBrowser(new MessageSource.Queue("orders", IsDlq: true), new FakeEmulatorNamespace(new FakeServices { Messages = service }));
        await browser.PeekAsync(250, null);
        browser.SelectAll();

        var retry = browser.RetryAsync(browser.Targets);
        await WaitUntilAsync(() => service.Completed.Count == 100);
        var op = browser.Operation.ShouldNotBeNull();
        op.Cancel();

        await retry.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        op.Result.ShouldNotBeNull().ShouldStartWith("Cancelled. Retried 100 of 250");
        op.Outcome.ShouldBe(OperationOutcome.Cancelled);
        service.Completed.Count.ShouldBe(100);
        browser.Messages.Count.ShouldBe(150);
        browser.Selected.Count.ShouldBe(150);

        browser.Dismiss();
        browser.Operation.ShouldBeNull();
    }

    private static readonly MessageSource.Queue OrdersDlq = new("orders", IsDlq: true);

    private static async Task<(MessageBrowser Browser, FakeServices Services)> PeekedOnStandardAsync(int count, Action<FakeServices>? arrange = null)
    {
        var services = new FakeServices();
        services.Messages.Messages.AddRange(Enumerable.Range(1, count).Select(i => Message(i)));
        arrange?.Invoke(services);
        var browser = new MessageBrowser(OrdersDlq, new FakeStandardNamespace(AppHarness.AzureNamespace, services));
        await browser.PeekAsync(count, null);
        browser.SelectAll();
        return (browser, services);
    }

    [Test]
    public async Task WithTransactions_MovesInOrder_InChunksOfAtMost100()
    {
        var (browser, services) = await PeekedOnStandardAsync(250);

        await browser.RetryAsync(browser.Targets).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        services.Transactions.Moved.ShouldAllBe(chunk => chunk.Count <= 100);
        services.Transactions.Moved.SelectMany(chunk => chunk).ShouldBe(Enumerable.Range(1, 250).Select(i => (long)i));
        services.Messages.Sent.ShouldBeEmpty();
        browser.Messages.ShouldBeEmpty();
        var op = browser.Operation.ShouldNotBeNull();
        op.Steps.Select(s => s.Name).ShouldBe(["PeekLock", "Move to orders"]);
        op.Result.ShouldBe("Retried 250 of 250.");
        op.Outcome.ShouldBe(OperationOutcome.Succeeded);
    }

    [Test]
    public async Task WithTransactions_AChunkThatRollsBack_StaysListedAndSelected_TheOthersMove()
    {
        var (browser, services) = await PeekedOnStandardAsync(250, s => s.Transactions.Fails = chunk => chunk.Any(m => m.SequenceNumber == 150));

        await browser.RetryAsync(browser.Targets).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        var rolledBack = services.Transactions.RolledBack.ShouldHaveSingleItem();
        rolledBack.ShouldContain(150L);
        services.Transactions.Moved.SelectMany(chunk => chunk).ShouldBe(Enumerable.Range(1, 250).Select(i => (long)i).Except(rolledBack));
        browser.Messages.Select(m => m.SequenceNumber).ShouldBe(rolledBack);
        browser.Selected.ShouldBe(rolledBack, ignoreOrder: true);
        var op = browser.Operation.ShouldNotBeNull();
        op.Outcome.ShouldBe(OperationOutcome.Failed);
        op.Result.ShouldNotBeNull().ShouldContain("not moved: transaction rolled back");
    }

    [Test]
    public async Task WithTransactions_CancellingKeepsTheRestSelected()
    {
        var (browser, services) = await PeekedOnStandardAsync(250, s =>
        {
            s.Messages.PauseScan = new TaskCompletionSource();
            s.Messages.PauseScanAfter = 100;
        });

        var retry = browser.RetryAsync(browser.Targets);
        await WaitUntilAsync(() => services.Transactions.Moved.Sum(chunk => chunk.Count) == 100);
        var op = browser.Operation.ShouldNotBeNull();
        op.Cancel();

        await retry.WaitAsync(TimeSpan.FromSeconds(10), Ct);
        op.Result.ShouldNotBeNull().ShouldStartWith("Cancelled. Retried 100 of 250");
        op.Outcome.ShouldBe(OperationOutcome.Cancelled);
        browser.Messages.Count.ShouldBe(150);
        browser.Selected.Count.ShouldBe(150);
    }

    [Test]
    public async Task WithTransactions_LockedMessagesMoveWithoutTheLockStep()
    {
        var services = new FakeServices();
        services.Messages.Messages.AddRange(Enumerable.Range(1, 120).Select(i => Message(i)));
        var browser = new MessageBrowser(OrdersDlq, new FakeStandardNamespace(AppHarness.AzureNamespace, services));
        await browser.ReceiveAsync(120, deleteOnReceive: false);
        browser.SelectAll();

        await browser.RetryAsync(browser.Targets).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        services.Transactions.Moved.Select(chunk => chunk.Count).ShouldBe([100, 20]);
        browser.Operation.ShouldNotBeNull().Steps.Select(s => s.Name).ShouldBe(["Move to orders"]);
        browser.Messages.ShouldBeEmpty();
    }

    [Test]
    public async Task WithoutTransactions_SendsThenCompletes()
    {
        var services = new FakeServices();
        services.Messages.Messages.AddRange(Enumerable.Range(1, 3).Select(i => Message(i)));
        var browser = new MessageBrowser(OrdersDlq, new FakeBasicNamespace(AppHarness.AzureNamespace with { Sku = "Basic" }, services));
        await browser.PeekAsync(3, null);
        browser.SelectAll();

        await browser.RetryAsync(browser.Targets).WaitAsync(TimeSpan.FromSeconds(10), Ct);

        services.Transactions.Moved.ShouldBeEmpty();
        services.Messages.Sent.Select(s => s.SequenceNumber).ShouldBe([1L, 2L, 3L]);
        services.Messages.Completed.Order().ShouldBe([1L, 2L, 3L]);
        browser.Operation.ShouldNotBeNull().Steps.Select(s => s.Name).ShouldBe(["PeekLock", "Requeue to orders", "Mark complete"]);
    }

    private static MessageInfo Message(long sequence) =>
        new() { SequenceNumber = sequence, State = MessageState.Peeked, MessageId = $"msg-{sequence}", Body = "{}"u8.ToArray() };

    private static async Task WaitUntilAsync(Func<bool> condition)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (!condition())
        {
            if (DateTime.UtcNow > deadline) throw new TimeoutException("Condition not met within 10s");
            await Task.Delay(10, Ct);
        }
    }
}
