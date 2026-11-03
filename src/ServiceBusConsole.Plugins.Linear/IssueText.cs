using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.Linear;

/// <summary>The title and Markdown description of the issue made from a message.</summary>
internal static partial class IssueText
{
    private const int MaxTitleLength = 255;

    /// <summary>The title template filled in; a message without a subject reads <c>Message #42</c> for <c>{subject}</c>.</summary>
    public static string Title(MessageActionContext context, string template)
    {
        var number = $"Message #{context.Message.SequenceNumber.ToString(CultureInfo.InvariantCulture)}";
        var withSubject = context.Message.Subject.Length > 0 ? template : SubjectPlaceholder().Replace(template, number);
        var title = Template.Fill(withSubject, context).ReplaceLineEndings(" ").Trim() is { Length: > 0 } filled
            ? filled
            : $"{context.Source.DisplayName}: {number}";
        return title.Length <= MaxTitleLength ? title : title[..(MaxTitleLength - 1)] + "…";
    }

    public static string Description(MessageActionContext context, bool includeContext)
    {
        var intro = $"Created from [azsb](https://github.com/zidad/azure-servicebus-console) on `{context.Source.DisplayName}`"
            + (context.Session.Namespace is { } ns ? $" in namespace `{ns.Name}`" : "")
            + ".\n\n";
        var message = MessageMarkdown.Format(context.Message);
        return includeContext
            ? $"{intro}{message}\n{Demoted(SessionMarkdown.Format(context.Session with { Message = null }))}"
            : intro + message;
    }

    // The session's headings go one level under the issue's, beside the message's.
    private static string Demoted(string markdown) => Heading().Replace(markdown, "#$1");

    [GeneratedRegex(@"\{subject\}", RegexOptions.IgnoreCase)]
    private static partial Regex SubjectPlaceholder();

    [GeneratedRegex("^(#+ )", RegexOptions.Multiline)]
    private static partial Regex Heading();
}
