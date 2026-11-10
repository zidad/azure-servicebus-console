using System.Text;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.CommandLine;

/// <summary>Sends one new message to a queue or topic; the body from <c>--body</c>, <c>--file</c> or standard input.</summary>
public sealed record SendCommand(string Entity) : NamespaceCommand
{
    public string? Body { get; init; }
    public string? File { get; init; }
    public string MessageId { get; init; } = "";
    public string ContentType { get; init; } = "";
    public string Subject { get; init; } = "";
    public string CorrelationId { get; init; } = "";

    /// <summary><c>--property key=value</c>, repeatable.</summary>
    public IReadOnlyList<string> Properties { get; init; } = [];

    public override async Task<int> RunAsync(CommandContext context)
    {
        var properties = Properties.Select(ParseProperty).ToList();
        var body = await ReadBodyAsync(context);
        var ns = await context.OpenAsync();
        var target = await EntityPath.ResolveSendTargetAsync(ns, Entity, context.Token);
        context.RequireConfirmation(ns.Info);

        var message = new OutgoingMessage
        {
            Body = body, MessageId = MessageId, ContentType = ContentType, Subject = Subject, CorrelationId = CorrelationId, Properties = properties,
        };
        await ns.Messages.SendAsync(target, [message], context.Token).LastOrDefaultAsync(context.Token);
        if (context.Json) await context.WriteJsonAsync(new JsonObject { ["sent"] = 1, ["to"] = target });
        else await context.Out.WriteLineAsync(CommandLineStrings.FormatSendCommand_Sent(target));
        return ExitCodes.Ok;
    }

    private async Task<ReadOnlyMemory<byte>> ReadBodyAsync(CommandContext context)
    {
        if (Body is not null && File is not null) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.SendCommand_OneBody);
        if (Body is { } text) return Encoding.UTF8.GetBytes(text);
        if (File is { } path)
        {
            try
            {
                return await System.IO.File.ReadAllBytesAsync(path, context.Token);
            }
            catch (IOException ex)
            {
                throw new CommandFailure(ExitCodes.NotFound, ex.Message);
            }
        }
        if (context.Services.Input() is not { } input) throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.SendCommand_NoBody);
        using var buffer = new MemoryStream();
        await input.CopyToAsync(buffer, context.Token);
        return buffer.ToArray();
    }

    private static KeyValuePair<string, string> ParseProperty(string property) =>
        property.IndexOf('=') is > 0 and var eq
            ? new(property[..eq], property[(eq + 1)..])
            : throw new CommandFailure(ExitCodes.Usage, CommandLineStrings.FormatSendCommand_BadProperty(property));
}
