using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>How retry and delete report: what was done, what failed and why, and which requested messages weren't found.</summary>
public static class MessageOutcome
{
    /// <summary>Writes the outcome and returns the exit code: failed if any failed, else not found if any was missing.</summary>
    public static async Task<int> ReportAsync(
        CommandContext context, Func<object?, object?, object?, string> summary, Func<object?, object?, string> dryRunSummary, MessageSource source, MessageSelection selection, MessageSelection.Found found,
        IReadOnlyList<Failure> failures, bool dryRun)
    {
        var failed = failures.Select(f => f.Message.SequenceNumber).ToHashSet();
        var done = found.Targets.Where(m => !failed.Contains(m.SequenceNumber)).ToList();
        if (context.Json)
        {
            await context.WriteJsonAsync(new JsonObject
            {
                ["source"] = source.DisplayName,
                ["dryRun"] = dryRun,
                [dryRun ? "matched" : "done"] = new JsonArray([.. done.Select(m => (JsonNode)Ids(m))]),
                ["failed"] = new JsonArray([.. failures.Select(f => { var json = Ids(f.Message); json["reason"] = f.Reason; return (JsonNode)json; })]),
                ["missing"] = new JsonObject
                {
                    ["messageIds"] = new JsonArray([.. found.MissingIds.Select(id => (JsonNode?)id)]),
                    ["sequenceNumbers"] = new JsonArray([.. found.MissingSequenceNumbers.Select(s => (JsonNode?)s)]),
                },
            });
        }
        else
        {
            var lines = dryRun
                ? [dryRunSummary(found.Targets.Count, source.DisplayName), .. done.Select(m => $"  #{m.SequenceNumber}  {m.MessageId}")]
                : new List<string> { summary(done.Count, found.Targets.Count, source.DisplayName) };
            lines.AddRange(failures.OrderBy(f => f.Message.SequenceNumber).Select(f => $"  ✗ #{f.Message.SequenceNumber}  {f.Message.MessageId}: {f.Reason}"));
            if (found.AnyMissing)
                lines.Add(CommandLineStrings.FormatMessageOutcome_Missing(selection.Limit,
                    string.Join(", ", found.MissingIds.Concat(found.MissingSequenceNumbers.Select(s => $"#{s}")))));
            foreach (var line in lines) await context.Out.WriteLineAsync(line);
        }
        return failures.Count > 0 ? ExitCodes.Failed : found.AnyMissing ? ExitCodes.NotFound : ExitCodes.Ok;
    }

    private static JsonObject Ids(MessageInfo m) => new() { ["sequenceNumber"] = m.SequenceNumber, ["messageId"] = m.MessageId };

    /// <summary>Exactly one of IDs/sequence numbers or --all.</summary>
    public static void Validate(MessageSelection selection)
    {
        if (selection.IsEmpty || (selection.All && (selection.MessageIds.Count > 0 || selection.SequenceNumbers.Count > 0)))
            throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.MessageOutcome_Selection);
        if (selection.Limit <= 0) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.MessageOutcome_Limit);
    }
}
