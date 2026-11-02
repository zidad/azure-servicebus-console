namespace ServiceBusConsole.Plugins.Linear;

/// <summary>Linear refused or failed a call; the message is meant for the user.</summary>
public sealed class LinearException(string message) : Exception(message);
