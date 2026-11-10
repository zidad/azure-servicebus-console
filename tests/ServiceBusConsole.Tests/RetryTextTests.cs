namespace ServiceBusConsole.Tests;

public sealed class RetryTextTests
{
    [Test]
    public void Fields_PeekedWithTransactions_MoveFromTheDeadLetterQueueInTransactions()
    {
        var fields = RetryText.Fields(BrowseMode.Peek, new MessageSource.Queue("orders", IsDlq: true), transactional: true);

        fields.Select(f => f.Label).ShouldBe(["From", "To", "How", "Note"]);
        fields[0].Value.ShouldBe("orders DLQ");
        fields[1].Value.ShouldBe("orders");
        fields[2].Value.ShouldStartWith("PeekLock → Move to orders\nUp to 100 per transaction");
        fields[3].Value.ShouldStartWith("Messages ahead of them are locked briefly");
        fields[3].Warning.ShouldBeTrue();
    }

    [Test]
    public void Fields_LockedWithoutTransactions_SendThenComplete()
    {
        var fields = RetryText.Fields(BrowseMode.Locked, new MessageSource.Queue("orders", IsDlq: true), transactional: false);

        fields.Select(f => f.Label).ShouldBe(["From", "To", "How"]);
        fields[2].Value.ShouldStartWith("Requeue to orders → Mark complete\nSent, then completed: at least once.");
    }

    [Test]
    public void Fields_ReceivedFromASubscription_RequeueToTheTopic()
    {
        var fields = RetryText.Fields(BrowseMode.Received, new MessageSource.Subscription("events", "audit"), transactional: true);

        fields.Select(f => (f.Label, f.Value)).ShouldBe(
        [
            ("From", "events/audit"),
            ("To", "events"),
            ("How", "Requeue to events"),
            ("Note", "Receive-and-delete already removed the originals."),
            ("", "Every subscription of the topic gets a copy."),
        ]);
    }

    [Test]
    public void ResendFields_FromAQueue_SendCopiesAndKeepTheOriginals()
    {
        var fields = RetryText.ResendFields(new MessageSource.Queue("orders"));

        fields.Select(f => (f.Label, f.Value, f.Warning)).ShouldBe(
        [
            ("From", "orders", false),
            ("To", "orders", false),
            ("How", "Send copies to orders", false),
            ("Note", "The originals stay in orders.", true),
        ]);
    }

    [Test]
    public void ResendFields_FromASubscription_SendToTheTopic()
    {
        var fields = RetryText.ResendFields(new MessageSource.Subscription("events", "audit"));

        fields.Select(f => (f.Label, f.Value)).ShouldBe(
        [
            ("From", "events/audit"),
            ("To", "events"),
            ("How", "Send copies to events"),
            ("Note", "The originals stay in events/audit."),
            ("", "Every subscription of the topic gets a copy."),
        ]);
    }
}
