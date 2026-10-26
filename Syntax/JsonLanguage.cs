using ColorCode;
using ColorCode.Common;

namespace ServiceBusConsole.Syntax;

/// <summary>
/// ColorCode 2.0.15's JSON grammar with the string pattern from ColorCode-Universal PR #46. The shipped
/// pattern backtracks exponentially on a string not followed by <c>:</c> (any long string after the first
/// in an array) and hangs the render thread. Drop this once a ColorCode release has the fix.
/// </summary>
public sealed class JsonLanguage : ILanguage
{
    private const string String = """
        "[^"\\]*(?:\\.[^"\\]*)*"
        """;

    public string Id => LanguageId.Json;

    public string FirstLinePattern => string.Empty;

    public string Name => "JSON";

    public string CssClassName => "json";

    public IList<LanguageRule> Rules { get; } = new List<LanguageRule>
    {
        new(@"[,\{]\s*(" + String + @")\s*:", new Dictionary<int, string> { { 1, ScopeName.JsonKey } }),
        new(String, new Dictionary<int, string> { { 0, ScopeName.JsonString } }),
        new(@"-?(?:0|[1-9][0-9]*)(?:\.[0-9]*)?(?:[eE][-+]?[0-9]+)?", new Dictionary<int, string> { { 0, ScopeName.JsonNumber } }),
        new(@"\b(true|false|null)\b", new Dictionary<int, string> { { 1, ScopeName.JsonConst } }),
    };

    public bool HasAlias(string lang) => string.Equals(lang, "json", StringComparison.OrdinalIgnoreCase);
}
