using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.Tests;

public sealed class RetryPipelineTests
{
    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task Retry_CompletesTheFirstChunk_WhileTheScanIsStillRunning()
    {
        var service = new FakeMessageService { PauseScan = new TaskCompletionSource(), PauseScanAfter = 100 };
        service.Messages.AddRange(Enumerable.Range(1, 250).Select(i => Message(i)));
        var browser = new MessageBrowser(new MessageSource("orders", IsDlq: true), service);
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

    [Fact]
    public async Task CancellingRetry_CompletesWhatWasSent_AndKeepsTheRestSelected()
    {
        var service = new FakeMessageService { PauseScan = new TaskCompletionSource(), PauseScanAfter = 100 };
        service.Messages.AddRange(Enumerable.Range(1, 250).Select(i => Message(i)));
        var browser = new MessageBrowser(new MessageSource("orders", IsDlq: true), service);
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

    private static MessageInfo Message(long sequence) => new(
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString("{}"), messageId: $"msg-{sequence}", sequenceNumber: sequence),
        MessageState.Peeked);

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
