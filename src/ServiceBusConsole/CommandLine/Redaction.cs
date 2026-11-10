using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// Hides message content before a command prints it, for namespaces matching <c>cli.redact.namespaces</c>. Each field in
/// <c>cli.redact.fields</c> is a dotted path matched against the end of a JSON property's path (array items don't count),
/// with <c>*</c> wildcards: <c>name</c> is every <c>name</c>, <c>identifier.value</c> a <c>value</c> inside an <c>identifier</c>.
/// A <c>:scalar</c> suffix redacts only text, numbers and booleans. Application properties are matched by their name alone.
/// A body that isn't JSON is withheld entirely.
/// </summary>
public sealed class Redaction
{
    public const string Redacted = "[redacted]";

    private readonly IReadOnlyList<Field> _fields;

    private Redaction(IReadOnlyList<Field> fields) => _fields = fields;

    public static Redaction None { get; } = new([]);

    public bool IsActive => _fields.Count > 0;

    /// <summary>The configured redaction when <paramref name="ns"/> matches <c>cli.redact.namespaces</c>, else <see cref="None"/>.</summary>
    public static Redaction For(NamespaceInfo ns, IConfiguration configuration) =>
        Glob.List(configuration[ConfigKeys.RedactNamespaces.ConfigurationPath]).Any(g => g.Matches(ns.Name) || g.Matches(ns.FullyQualifiedNamespace))
            ? Of(configuration[ConfigKeys.RedactFields.ConfigurationPath])
            : None;

    /// <summary>Fields as the setting lists them, comma-separated. Redaction without fields still withholds non-JSON bodies.</summary>
    public static Redaction Of(string? fields) =>
        new([.. (fields ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Select(Field.Parse), Field.Never]);

    /// <summary>A message as <see cref="MessageJson.ToNode"/> gives it, with its body and properties redacted.</summary>
    public JsonObject Apply(JsonObject message)
    {
        if (!IsActive) return message;
        if (message["properties"] is JsonObject properties)
            foreach (var key in properties.Select(p => p.Key).Where(key => _fields.Any(f => f.Matches([key], scalar: true))).ToList())
                properties[key] = Redacted;
        message["body"] = message["body"] switch
        {
            JsonObject or JsonArray => Walk(message["body"]!.DeepClone(), []),
            _ => CommandLineStrings.Redaction_BodyWithheld,
        };
        return message;
    }

    private JsonNode Walk(JsonNode node, IReadOnlyList<string> path)
    {
        switch (node)
        {
            case JsonObject obj:
                foreach (var (key, value) in obj.ToList())
                {
                    string[] childPath = [.. path, key];
                    var scalar = value is not (JsonObject or JsonArray);
                    if (_fields.Any(f => f.Matches(childPath, scalar))) obj[key] = Redacted;
                    else if (value is not null) Walk(value, childPath);
                }
                break;
            case JsonArray array:
                foreach (var item in array.OfType<JsonNode>()) Walk(item, path);
                break;
        }
        return node;
    }

    private sealed record Field(IReadOnlyList<Glob> Segments, bool ScalarOnly)
    {
        // Keeps the list non-empty, so a namespace with redaction but no fields still withholds non-JSON bodies.
        public static Field Never { get; } = new([], ScalarOnly: false);

        private const string ScalarSuffix = ":scalar";

        public static Field Parse(string spec)
        {
            var scalarOnly = spec.EndsWith(ScalarSuffix, StringComparison.OrdinalIgnoreCase);
            var path = scalarOnly ? spec[..^ScalarSuffix.Length] : spec;
            return new([.. path.Split('.', StringSplitOptions.RemoveEmptyEntries).Select(s => new Glob(s))], scalarOnly);
        }

        public bool Matches(IReadOnlyList<string> path, bool scalar) =>
            Segments.Count > 0 && Segments.Count <= path.Count && (scalar || !ScalarOnly)
            && Segments.Select((glob, i) => glob.Matches(path[path.Count - Segments.Count + i])).All(m => m);
    }
}
