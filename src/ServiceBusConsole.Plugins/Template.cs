using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins;

/// <summary>
/// Fills <c>{placeholders}</c> from a message, e.g. a configurable issue title: <c>{subject}</c>, <c>{messageId}</c>,
/// <c>{key}</c>, <c>{entity}</c>, <c>{namespace}</c>, <c>{deadLetterReason}</c>. The transport's own fields by their ID (Service Bus: <c>{sequenceNumber}</c>). Names ignore case; unknown ones stay.
/// </summary>
public static partial class Template
{
    public static string Fill(string template, MessageActionContext context) =>
        Placeholder().Replace(template, match => Value(match.Groups[1].Value, context) ?? match.Value);

    private static string? Value(string name, MessageActionContext context) => name.ToLowerInvariant() switch
    {
        "subject" => context.Message.Subject,
        "messageid" => context.Message.MessageId,
        "key" => context.Message.Key.Value,
        "entity" => context.Source.DisplayName,
        "namespace" => context.Session.Broker?.Name ?? "",
        "deadletterreason" => context.Message.DeadLetterReason ?? "",
        var other => context.Message.TransportProperties.FirstOrDefault(p => p.Id.Equals(other, StringComparison.OrdinalIgnoreCase))?.Text,
    };

    [GeneratedRegex(@"\{([A-Za-z]+)\}")]
    private static partial Regex Placeholder();
}
