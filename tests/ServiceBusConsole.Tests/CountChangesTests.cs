namespace ServiceBusConsole.Tests;

public sealed class CountChangesTests
{
    private sealed class ManualTime : TimeProvider
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UnixEpoch;
        public override DateTimeOffset GetUtcNow() => Now;
    }

    private readonly ManualTime _time = new();

    private CountChanges<QueueInfo> Changes() => new(q => q.Name,
        ("Active", q => q.ActiveMessageCount), ("DLQ", q => q.DeadLetterMessageCount)) { Time = _time };

    [Fact]
    public void HighlightsOnlyChangedColumns_ThenFades()
    {
        var changes = Changes();
        var before = new QueueInfo { Name = "orders", ActiveMessageCount = 1, DeadLetterMessageCount = 2 };
        var after = new QueueInfo { Name = "orders", ActiveMessageCount = 5, DeadLetterMessageCount = 2 };

        changes.Compare(before, after);

        changes.Highlight(after, "Active").ShouldBe(Theme.Changed);
        changes.Highlight(after, "DLQ").ShouldBeNull();
        changes.IsFading().ShouldBeTrue();

        _time.Now += CountChanges<QueueInfo>.Fade / 2;
        var half = changes.Highlight(after, "Active");
        half.ShouldNotBeNull();
        half.ShouldNotBe(Theme.Changed);
        half.ShouldNotBe(Theme.ChangedFadeTo);

        _time.Now += CountChanges<QueueInfo>.Fade / 2;
        changes.Highlight(after, "Active").ShouldBeNull();
        changes.IsFading().ShouldBeFalse();
    }

    [Fact]
    public void ANewChange_RestartsTheFade()
    {
        var changes = Changes();
        var a = new QueueInfo { Name = "orders", ActiveMessageCount = 1 };
        var b = new QueueInfo { Name = "orders", ActiveMessageCount = 2 };
        var c = new QueueInfo { Name = "orders", ActiveMessageCount = 3 };

        changes.Compare(a, b);
        _time.Now += CountChanges<QueueInfo>.Fade * 0.9;
        changes.Compare(b, c);

        changes.Highlight(c, "Active").ShouldBe(Theme.Changed);
    }
}
