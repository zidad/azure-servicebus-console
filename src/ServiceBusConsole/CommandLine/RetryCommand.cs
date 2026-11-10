namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Sends dead-lettered messages back to their queue or topic and removes them from the dead-letter queue, like Retry in
/// the UI: locked first, moved in transactions where the namespace has them. Only the messages present when it starts,
/// so one that fails again is not retried in a loop.
/// </summary>
public sealed record RetryCommand(string Entity, MessageSelection Selection, bool DryRun = false) : NamespaceCommand
{
    public override async Task<int> RunAsync(CommandContext context)
    {
        MessageOutcome.Validate(Selection);
        var ns = await context.OpenAsync();
        var source = await EntityPath.ResolveAsync(ns, Entity, dlq: true, context.Token);
        if (!source.IsDlq) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.RetryCommand_NeedsDlq);
        var found = await Selection.FindAsync(ns.Messages, source, context.Token);
        if (!DryRun) context.RequireConfirmation(ns.Info);

        IReadOnlyList<Failure> failures = [];
        if (!DryRun && found.Targets.Count > 0)
        {
            await using var browser = new MessageBrowser(source, ns);
            await browser.RetryAsync(found.Targets);
            failures = browser.Operation?.Failures ?? [];
        }
        return await MessageOutcome.ReportAsync(context, CommandLineStrings.FormatRetryCommand_Done, CommandLineStrings.FormatRetryCommand_DryRun, source, Selection, found, failures, DryRun);
    }
}
