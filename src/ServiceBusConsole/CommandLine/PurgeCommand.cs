using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>Deletes every message of a queue or subscription, or of its dead-letter queue; needs <c>--yes</c>.</summary>
public sealed record PurgeCommand(string Entity, bool Dlq, bool Yes) : NamespaceCommand
{
    public override async Task<int> RunAsync(CommandContext context)
    {
        if (!Yes) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.PurgeCommand_NeedsYes);
        var ns = await context.OpenAsync();
        var source = await EntityPath.ResolveAsync(ns, Entity, Dlq, context.Token);
        context.RequireConfirmation(ns.Info);

        await using var browser = new MessageBrowser(source, ns);
        var count = await browser.PurgeAsync();
        var ok = browser.Operation is not { Outcome: not OperationOutcome.Succeeded };
        if (context.Json)
            await context.WriteJsonAsync(new JsonObject { ["source"] = source.DisplayName, ["purged"] = count, ["ok"] = ok, ["result"] = browser.Operation?.Result });
        else
            await context.Out.WriteLineAsync(browser.Operation?.Result ?? CommandLineStrings.FormatPurgeCommand_Purged(count, source.DisplayName));
        return ok ? ExitCodes.Ok : ExitCodes.Failed;
    }
}
