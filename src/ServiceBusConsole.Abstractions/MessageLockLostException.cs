using ServiceBusConsole.Abstractions.Resources;

namespace ServiceBusConsole.Abstractions;

/// <summary>The PeekLock on a message expired or was lost, so it can no longer be settled.</summary>
public sealed class MessageLockLostException(long sequenceNumber, Exception? innerException = null)
    : Exception(Strings.FormatMessageLockLostException_Message(sequenceNumber), innerException);
