using System.Globalization;
using System.Text;

namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>The issue body as Markdown: where it came from, the message, and optionally the session's context and activity.</summary>
internal static class IssueBody
{
    /// <summary>GitHub rejects longer issue bodies.</summary>
    public const int MaxLength = 65_536;

    public static string Format(MessageActionContext context, bool includeContext)
    {
        var full = Compose(context, context.Message, includeContext);
        if (full.Length <= MaxLength) return full;

        var bodyText = context.Message.BodyText;
        var note = Note(bodyText.Length);
        var keep = bodyText.Length - (full.Length - MaxLength) - note.Length;
        var shortened = keep > 0
            ? Compose(context, context.Message with { Body = Encoding.UTF8.GetBytes(Cut(bodyText, keep)) }, includeContext) + note
            : "";
        // Without room in the message body (huge properties or activity), cut the whole text.
        return shortened.Length is > 0 and <= MaxLength ? shortened : Cut(full, MaxLength - note.Length) + note;
    }

    private static string Compose(MessageActionContext context, MessageInfo message, bool includeContext)
    {
        var sb = new StringBuilder()
            .Append(CultureInfo.InvariantCulture,
                $"Created with [azsb](https://github.com/zidad/azure-servicebus-console) from message #{message.SequenceNumber} in `{context.Source.DisplayName}`.\n\n")
            .Append(MessageMarkdown.Format(message));
        if (includeContext) sb.Append('\n').Append(Demoted((context.Session with { Message = null }).ToMarkdown()));
        return sb.ToString();
    }

    private static string Demoted(string markdown) =>
        string.Join('\n', markdown.Split('\n').Select(line => line.StartsWith('#') ? $"#{line}" : line));

    private static string Note(int bodyLength) =>
        string.Create(CultureInfo.InvariantCulture,
            $"\n\n> [!NOTE]\n> Truncated to fit GitHub's limit of {MaxLength:N0} characters; the message body has {bodyLength:N0}.\n");

    private static string Cut(string text, int length) =>
        length >= text.Length ? text
        : length <= 0 ? ""
        : text[..(char.IsHighSurrogate(text[length - 1]) ? length - 1 : length)];
}
