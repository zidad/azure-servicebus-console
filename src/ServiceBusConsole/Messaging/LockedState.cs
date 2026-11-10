namespace ServiceBusConsole.Messaging;

/// <summary>Where a message received with PeekLock stands: still held, settled by this console, or its lock lost.</summary>
public enum LockedState { Held, Completed, Abandoned, DeadLettered, LockLost }
