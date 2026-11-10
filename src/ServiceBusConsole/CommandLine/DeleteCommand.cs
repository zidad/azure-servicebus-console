namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Removes dead-lettered messages for good, chosen by ID or sequence number. Only in a dead-letter queue: finding a message
/// means receiving the ones before it, which in an active queue would dead-letter unrelated messages.
/// </summary>
public sealed record DeleteCommand(string Entity, bool Dlq, MessageSelection Selection, bool DryRun = false) : NamespaceCommand
{
    public override async Task<int> RunAsync(CommandContext context)
    {
        MessageOutcome.Validate(Selection);
        var ns = await context.OpenAsync();
        var source = await EntityPath.ResolveAsync(ns, Entity, Dlq, context.Token);
        if (!source.IsDlq) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.DeleteCommand_NeedsDlq);
        var found = await Selection.FindAsync(ns.Messages, source, context.Token);
        if (!DryRun) context.RequireConfirmation(ns.Info);

        IReadOnlyList<Failure> failures = [];
        if (!DryRun && found.Targets.Count > 0)
        {
            await using var browser = new MessageBrowser(source, ns);
            await browser.DeleteAsync(found.Targets);
            failures = browser.Operation?.Failures ?? [];
        }
        return await MessageOutcome.ReportAsync(context, CommandLineStrings.FormatDeleteCommand_Done, CommandLineStrings.FormatDeleteCommand_DryRun, source, Selection, found, failures, DryRun);
    }
}
