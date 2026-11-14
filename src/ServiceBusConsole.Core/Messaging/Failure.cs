namespace ServiceBusConsole.Core.Messaging;

/// <summary>A message a batch operation could not handle, and why.</summary>
public readonly record struct Failure(MessageInfo Message, string Reason)
{
    public static string CancelledReason => MessagingStrings.Failure_Cancelled;

    public bool IsCancelled => Reason == CancelledReason;
}
