using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.Abstractions;

/// <summary>Culture-invariant text shared by the Markdown and JSON formats.</summary>
internal static class ContextText
{
    // Relaxed escaping keeps quotes, accents and emoji readable; the output is never embedded in HTML.
    public static readonly JsonSerializerOptions Indented = new()
    {
        WriteIndented = true, NewLine = "\n", Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string Time(DateTimeOffset time) =>
        time.ToString("yyyy-MM-dd'T'HH:mm:ss.fffzzz", CultureInfo.InvariantCulture);

    /// <summary>The body parsed, when it is a JSON object or array, also sent as an AMQP string (JSON-encoded once), as the preview shows it.</summary>
    public static JsonNode? JsonBody(MessageInfo message) => JsonBody(message.BodyText, unwrap: true);

    private static JsonNode? JsonBody(string body, bool unwrap)
    {
        var text = body.Trim();
        try
        {
            return text switch
            {
                ['{' or '[', ..] => JsonNode.Parse(text),
                ['"', ..] when unwrap && JsonNode.Parse(text)?.GetValue<string>() is { } inner => JsonBody(inner, unwrap: false),
                _ => null,
            };
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
