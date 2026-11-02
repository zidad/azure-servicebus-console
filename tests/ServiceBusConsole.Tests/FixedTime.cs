namespace ServiceBusConsole.Tests;

/// <summary>A clock that stays where a test sets it.</summary>
public sealed class FixedTime(DateTimeOffset now) : TimeProvider
{
    public DateTimeOffset Now { get; set; } = now;

    public override DateTimeOffset GetUtcNow() => Now;
}
