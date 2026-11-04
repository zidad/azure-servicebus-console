using System.Globalization;
using System.Text;

namespace ServiceBusConsole.Abstractions;

/// <summary>A message as Markdown, for issue bodies and LLM prompts: fields, application properties and body.</summary>
public static class MessageMarkdown
{
    /// <param name="message">The message.</param>
    /// <param name="headingLevel">Level of the message's own heading; its sections go one deeper.</param>
    public static string Format(MessageInfo message, int headingLevel = 2)
    {
        var heading = new string('#', Math.Clamp(headingLevel, 1, 5));
        var sb = new StringBuilder()
            .Append(CultureInfo.InvariantCulture, $"{heading} Message #{message.SequenceNumber}\n\n")
            .Append(Bullet("Message ID", Code(message.MessageId)))
            .Append(Bullet("Sequence number", message.SequenceNumber.ToString(CultureInfo.InvariantCulture)))
            .Append(Bullet("State", message.State.ToString()))
            .Append(Bullet("Subject", Text(message.Subject)))
            .Append(Bullet("Content type", Text(message.ContentType)))
            .Append(Bullet("Enqueued", ContextText.Time(message.EnqueuedTime)))
            .Append(Bullet("Delivery count", message.DeliveryCount.ToString(CultureInfo.InvariantCulture)));
        if (message.DeadLetterReason is { } reason) sb.Append(Bullet("Dead-letter reason", Text(reason)));
        if (message.DeadLetterErrorDescription is { } description) sb.Append(Bullet("Dead-letter description", Text(description)));

        sb.Append(CultureInfo.InvariantCulture, $"\n{heading}# Application properties\n\n");
        if (message.Properties.Count == 0) sb.Append("None.\n");
        else sb.Append("| Name | Value |\n|---|---|\n")
            .AppendJoin("", message.Properties.Select(p => $"| {Cell(p.Key)} | {Cell(p.Value)} |\n"));

        sb.Append(CultureInfo.InvariantCulture, $"\n{heading}# Body\n\n");
        if (message.Body.IsEmpty) return sb.Append("Empty.\n").ToString();
        var (language, body) = ContextText.JsonBody(message) is { } json
            ? ("json", json.ToJsonString(ContextText.Indented))
            : ("", message.BodyText.TrimEnd('\n', '\r'));
        var fence = Fence(body);
        return sb.Append(CultureInfo.InvariantCulture, $"{fence}{language}\n{body}\n{fence}\n").ToString();
    }

    private static string Bullet(string name, string value) => $"- {name}: {value}\n";

    private static string Text(string value) => value.Length == 0 ? "(none)" : value.ReplaceLineEndings(" ");

    private static string Code(string value) => value.Length == 0 ? "(none)" : $"`{value}`";

    private static string Cell(string value) => value.ReplaceLineEndings(" ").Replace("|", "\\|");

    // Longer than any backtick run in the body, so the body cannot close it.
    private static string Fence(string body)
    {
        var longest = 0;
        var run = 0;
        foreach (var c in body)
        {
            run = c == '`' ? run + 1 : 0;
            longest = Math.Max(longest, run);
        }
        return new string('`', Math.Max(3, longest + 1));
    }
}
