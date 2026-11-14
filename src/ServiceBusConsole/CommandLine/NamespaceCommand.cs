namespace ServiceBusConsole.CommandLine;

/// <summary>What a namespace command does; returns its exit code (<see cref="ExitCodes"/>).</summary>
public abstract record NamespaceCommand
{
    public abstract Task<int> RunAsync(CommandContext context);

    /// <summary>Runs <paramref name="input"/>; failures go to standard error as one line and set the exit code.</summary>
    public static async Task<int> ExecuteAsync(NamespaceInput input, CancellationToken ct)
    {
        CommandServices services;
        try
        {
            services = input.Services();
        }
        catch (CommandFailure failure)
        {
            await Console.Error.WriteLineAsync(failure.Message);
            return failure.ExitCode;
        }
        await using var context = new CommandContext(input, services, ct);
        try
        {
            return await input.Command.RunAsync(context);
        }
        catch (CommandFailure failure)
        {
            await services.Error.WriteLineAsync(failure.Message);
            return failure.ExitCode;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            await services.Error.WriteLineAsync(CommandLineStrings.NamespaceCommand_Cancelled);
            return ExitCodes.Failed;
        }
        catch (Exception ex)
        {
            await services.Error.WriteLineAsync(ErrorText.Of(ex).Split('\n')[0]);
            return ExitCodes.Failed;
        }
    }
}
