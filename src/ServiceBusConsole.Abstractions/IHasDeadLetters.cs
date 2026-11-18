namespace ServiceBusConsole.Abstractions;

/// <summary>A broker that keeps the messages it could not deliver, per queue or subscription, to inspect and retry.</summary>
public interface IHasDeadLetters
{
    IDeadLetterService DeadLetters { get; }
}
