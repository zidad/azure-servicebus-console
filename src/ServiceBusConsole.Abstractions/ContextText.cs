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

    /// <summary>The body parsed, when it is a JSON object or array.</summary>
    public static JsonNode? JsonBody(MessageInfo message)
    {
        var text = message.BodyText.Trim();
        if (text is not ['{' or '[', ..]) return null;
        try
        {
            return JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }
}
