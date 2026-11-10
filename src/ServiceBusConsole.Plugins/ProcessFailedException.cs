using ServiceBusConsole.Plugins.Resources;

namespace ServiceBusConsole.Plugins;

/// <summary>A process exited with an error; the message ends with its first line of output, standard error first.</summary>
public sealed class ProcessFailedException(string command, ProcessResult result)
    : Exception(Strings.FormatProcessFailedException_Message(command, result.ExitCode, FirstLine(result)))
{
    public string Command { get; } = command;
    public ProcessResult Result { get; } = result;

    private static string FirstLine(ProcessResult result) =>
        result.StandardError.Split('\n').Concat(result.StandardOutput.Split('\n'))
            .Select(line => line.Trim())
            .FirstOrDefault(line => line.Length > 0)
        ?? Strings.ProcessFailedException_NoOutput;
}
