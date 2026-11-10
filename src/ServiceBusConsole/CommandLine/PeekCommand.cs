using System.Globalization;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>Prints messages without changing them; redacted where <c>cli.redact.namespaces</c> says so.</summary>
public sealed record PeekCommand(string Entity, bool Dlq = false, int Max = PeekCommand.DefaultMax, long? FromSequenceNumber = null, bool Full = false) : NamespaceCommand
{
    public const int DefaultMax = 20;
    private const int BodyLimit = 4096;

    public override async Task<int> RunAsync(CommandContext context)
    {
        var ct = context.Token;
        var ns = await context.OpenAsync();
        var source = await EntityPath.ResolveAsync(ns, Entity, Dlq, ct);
        var redaction = context.Redaction(ns.Info);
        var messages = await ns.Messages.PeekAsync(source, Max, FromSequenceNumber, ct).SelectMany(page => page.ToAsyncEnumerable()).ToListAsync(ct);
        var nodes = messages.Select(m => redaction.Apply(MessageJson.ToNode(m))).ToList();

        if (redaction.IsActive) await context.Error.WriteLineAsync(CommandLineStrings.FormatPeekCommand_Redacted(ns.Info.Name));
        if (context.Json)
        {
            await context.WriteJsonAsync(new JsonArray([.. nodes]));
            return ExitCodes.Ok;
        }
        await context.Error.WriteLineAsync(CommandLineStrings.FormatPeekCommand_Peeked(messages.Count, source.DisplayName));
        foreach (var node in nodes) await WriteAsync(context.Out, node, Full);
        return ExitCodes.Ok;
    }

    private static async Task WriteAsync(TextWriter writer, JsonObject m, bool full)
    {
        await writer.WriteLineAsync();
        await writer.WriteLineAsync($"#{m["sequenceNumber"]}  {m["messageId"]}");
        IEnumerable<(string Label, string? Value)> fields =
        [
            (CommandLineStrings.PeekCommand_Enqueued, Text(m["enqueuedTime"])),
            (CommandLineStrings.PeekCommand_DeliveryCount, Text(m["deliveryCount"])),
            (CommandLineStrings.PeekCommand_Subject, Text(m["subject"])),
            (CommandLineStrings.PeekCommand_ContentType, Text(m["contentType"])),
            (CommandLineStrings.PeekCommand_Reason, Text(m["deadLetterReason"])),
            (CommandLineStrings.PeekCommand_Description, Text(m["deadLetterErrorDescription"])),
        ];
        var shown = fields.Where(f => !string.IsNullOrEmpty(f.Value)).ToList();
        var width = shown.Max(f => f.Label.Length);
        foreach (var (label, value) in shown) await writer.WriteLineAsync($"  {label.PadRight(width)}  {value}");

        if (m["properties"] is JsonObject { Count: > 0 } properties)
        {
            await writer.WriteLineAsync($"  {CommandLineStrings.PeekCommand_Properties}");
            var keyWidth = properties.Max(p => p.Key.Length);
            foreach (var (key, property) in properties) await writer.WriteLineAsync($"    {key.PadRight(keyWidth)}  {Text(property)}");
        }

        var body = m["body"] is JsonValue bodyValue && bodyValue.TryGetValue<string>(out var text) ? text : CommandContext.Format(m["body"]);
        if (!full && body.Length > BodyLimit)
            body = body[..BodyLimit] + "\n" + CommandLineStrings.FormatPeekCommand_Truncated((body.Length - BodyLimit).ToString(CultureInfo.InvariantCulture));
        await writer.WriteLineAsync($"  {CommandLineStrings.PeekCommand_Body}");
        foreach (var line in body.Split('\n')) await writer.WriteLineAsync($"    {line}");
    }

    private static string? Text(JsonNode? node) => node switch
    {
        null => null,
        JsonValue v when v.TryGetValue<string>(out var s) => s,
        _ => node.ToJsonString(),
    };
}
