namespace ServiceBusConsole.CommandLine;

/// <summary>Ends a namespace command with <paramref name="exitCode"/>; the message goes to standard error.</summary>
public sealed class CommandFailure(int exitCode, string message) : Exception(message)
{
    public int ExitCode { get; } = exitCode;
}
