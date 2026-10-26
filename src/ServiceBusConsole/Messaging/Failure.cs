namespace ServiceBusConsole.Messaging;

/// <summary>A message a batch operation could not handle, and why.</summary>
public readonly record struct Failure(MessageInfo Message, string Reason)
{
    public const string CancelledReason = "cancelled";

    public bool IsCancelled => Reason == CancelledReason;
}
