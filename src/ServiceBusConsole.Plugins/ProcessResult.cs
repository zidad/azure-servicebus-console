namespace ServiceBusConsole.Plugins;

public sealed record ProcessResult(int ExitCode, string StandardOutput, string StandardError)
{
    public bool Succeeded => ExitCode == 0;

    /// <summary>This result when the exit code is 0; else throws <see cref="ProcessFailedException"/> naming <paramref name="command"/>.</summary>
    public ProcessResult EnsureSuccess(string command) => Succeeded ? this : throw new ProcessFailedException(command, this);
}
