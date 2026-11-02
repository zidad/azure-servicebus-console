namespace ServiceBusConsole.Tests;

public sealed class ActivityLogTests
{
    private static readonly DateTimeOffset Start = new(2026, 11, 2, 9, 0, 0, TimeSpan.Zero);

    [Test]
    public void Record_StampsTheTime_NewestLast()
    {
        var time = new FixedTime(Start);
        var log = new ActivityLog(time);
        var source = new MessageSource.Queue("orders");

        log.Record(ActivityKind.Navigated, "Opened /queues");
        time.Now = Start.AddSeconds(5);
        log.Record(ActivityKind.Loaded, "Peeked 3 message(s)", source);

        log.Entries.ShouldBe(
        [
            new(Start, ActivityKind.Navigated, "Opened /queues"),
            new(Start.AddSeconds(5), ActivityKind.Loaded, "Peeked 3 message(s)", source),
        ]);
    }

    [Test]
    public void Record_KeepsOnlyTheLastEntries()
    {
        var log = new ActivityLog(new FixedTime(Start), capacity: 3);

        foreach (var i in Enumerable.Range(1, 5)) log.Record(ActivityKind.Operation, $"#{i}");

        log.Entries.Select(e => e.Text).ShouldBe(["#3", "#4", "#5"]);
    }

    [Test]
    public void ByDefault_KeepsTwoHundred()
    {
        var log = new ActivityLog();

        foreach (var i in Enumerable.Range(1, 250)) log.Record(ActivityKind.Operation, $"#{i}");

        log.Entries.Count.ShouldBe(ActivityLog.DefaultCapacity);
        log.Entries[0].Text.ShouldBe("#51");
    }

    [Test]
    public async Task RecordingFromManyThreads_WhileReading_LosesNothingAndStaysBounded()
    {
        var log = new ActivityLog(new FixedTime(Start), capacity: 1000);
        using var reading = new CancellationTokenSource();
        var reader = Task.Run(() =>
        {
            while (!reading.IsCancellationRequested) log.Entries.Count.ShouldBeLessThanOrEqualTo(1000);
        }, Ct);

        await Task.WhenAll(Enumerable.Range(0, 8).Select(t => Task.Run(() =>
        {
            foreach (var i in Enumerable.Range(0, 500)) log.Record(ActivityKind.Operation, $"{t}:{i}");
        }, Ct)));
        await reading.CancelAsync();
        await reader;

        var entries = log.Entries;
        entries.Count.ShouldBe(1000);
        entries.Select(e => e.Text).Distinct().Count().ShouldBe(1000);
        // Each thread's entries stay in its own order.
        foreach (var thread in entries.GroupBy(e => e.Text.Split(':')[0]))
            thread.Select(e => int.Parse(e.Text.Split(':')[1])).ShouldBeInOrder();
    }

    [Test]
    public void ACapacityBelowOne_IsRejected() =>
        Should.Throw<ArgumentOutOfRangeException>(() => new ActivityLog(TimeProvider.System, capacity: 0));
}
