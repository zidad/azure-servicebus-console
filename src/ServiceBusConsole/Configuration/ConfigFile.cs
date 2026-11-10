using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Configuration;

/// <summary>
/// The JSON config file, read on every call so hand edits are never lost. Dotted keys (<c>ui.theme</c>) are nested
/// objects, the shape <c>IConfiguration</c> reads as <c>ui:theme</c>; they ignore case and are written in lower case.
/// </summary>
public sealed partial class ConfigFile(string filePath)
{
    private static readonly JsonNodeOptions NodeOptions = new() { PropertyNameCaseInsensitive = true };
    private static readonly JsonDocumentOptions DocumentOptions = new() { CommentHandling = JsonCommentHandling.Skip, AllowTrailingCommas = true };
    private static readonly JsonSerializerOptions WriteOptions = new() { WriteIndented = true, Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };

    public string FilePath { get; } = Path.GetFullPath(filePath);

    /// <summary>The value as text; null when unset or when the key names a section.</summary>
    public string? Get(string key) => Find(Load(), Segments(key)) switch
    {
        null or JsonObject => null,
        var node => Text(node),
    };

    /// <summary>Stores <c>true</c>/<c>false</c> as booleans, integers as numbers, anything else as a string.</summary>
    public void Set(string key, string value)
    {
        var segments = Segments(key);
        var root = Load();
        var parent = segments[..^1].Aggregate(root, (obj, segment) => obj[segment] as JsonObject ?? Put(obj, segment, new JsonObject(NodeOptions)));
        Put(parent, segments[^1], Typed(value));
        Save(root);
    }

    /// <summary>Removes a value and the sections it leaves empty; false when it isn't set. Never removes a whole section.</summary>
    public bool Unset(string key)
    {
        var segments = Segments(key);
        var root = Load();
        List<JsonObject> sections = [root];
        foreach (var segment in segments[..^1])
        {
            if (sections[^1][segment] is not JsonObject section) return false;
            sections.Add(section);
        }
        if (sections[^1][segments[^1]] is null or JsonObject) return false;

        sections[^1].Remove(segments[^1]);
        for (var i = sections.Count - 1; i > 0 && sections[i].Count == 0; i--)
            sections[i - 1].Remove(segments[i - 1]);
        Save(root);
        return true;
    }

    /// <summary>Every value, by key.</summary>
    public IReadOnlyList<ConfigEntry> List() => [.. Flatten(Load(), "").OrderBy(e => e.Key, StringComparer.OrdinalIgnoreCase)];

    private static IEnumerable<ConfigEntry> Flatten(JsonObject section, string prefix) =>
        section.SelectMany(property => property.Value switch
        {
            null => [],
            JsonObject child => Flatten(child, $"{prefix}{property.Key}."),
            var node => [new ConfigEntry(prefix + property.Key, Text(node))],
        });

    private static JsonNode? Find(JsonObject root, string[] segments) =>
        segments.Aggregate<string, JsonNode?>(root, (node, segment) => node is JsonObject section ? section[segment] : null);

    private static T Put<T>(JsonObject section, string name, T node) where T : JsonNode
    {
        section[name] = node;
        return node;
    }

    private static JsonNode Typed(string value) =>
        bool.TryParse(value, out var flag) ? JsonValue.Create(flag)
        : long.TryParse(value, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number)
          && number.ToString(CultureInfo.InvariantCulture) == value ? JsonValue.Create(number)
        : JsonValue.Create(value);

    private static string Text(JsonNode node) =>
        node is JsonValue value && value.GetValueKind() == JsonValueKind.String ? value.GetValue<string>() : node.ToJsonString();

    private static string[] Segments(string key) =>
        KeyPattern().IsMatch(key)
            ? key.ToLowerInvariant().Split('.')
            : throw new ArgumentException(ConfigurationStrings.FormatConfigFile_InvalidKey(key));

    private JsonObject Load()
    {
        if (!File.Exists(FilePath)) return new JsonObject(NodeOptions);
        var text = File.ReadAllText(FilePath);
        return string.IsNullOrWhiteSpace(text)
            ? new JsonObject(NodeOptions)
            : JsonNode.Parse(text, NodeOptions, DocumentOptions) as JsonObject
              ?? throw new InvalidDataException(ConfigurationStrings.FormatConfigFile_NotAnObject(FilePath));
    }

    private void Save(JsonObject root)
    {
        if (Path.GetDirectoryName(FilePath) is { } directory) Directory.CreateDirectory(directory);
        var temp = $"{FilePath}.{Environment.ProcessId}.tmp";
        File.WriteAllText(temp, root.ToJsonString(WriteOptions) + "\n");
        File.Move(temp, FilePath, overwrite: true);
    }

    [GeneratedRegex(@"^[A-Za-z0-9_-]+(\.[A-Za-z0-9_-]+)+$")]
    private static partial Regex KeyPattern();
}
