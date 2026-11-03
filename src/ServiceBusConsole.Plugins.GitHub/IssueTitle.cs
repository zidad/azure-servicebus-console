using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins.GitHub;

/// <summary>The issue title from the template; a message without a subject is named by its id, else its sequence number.</summary>
internal static partial class IssueTitle
{
    public const int MaxLength = 256;

    public static string Format(string template, MessageActionContext context)
    {
        var withSubject = context.Message.Subject.Length > 0
            ? template
            : Subject().Replace(template, context.Message.MessageId.Length > 0 ? "{messageId}" : "#{sequenceNumber}");
        var title = Template.Fill(withSubject, context).ReplaceLineEndings(" ").Trim() switch
        {
            "" => $"Message #{context.Message.SequenceNumber.ToString(CultureInfo.InvariantCulture)}",
            var filled => filled,
        };
        return title.Length <= MaxLength ? title : $"{title[..(MaxLength - 1)]}…";
    }

    [GeneratedRegex(@"\{subject\}", RegexOptions.IgnoreCase)]
    private static partial Regex Subject();
}
