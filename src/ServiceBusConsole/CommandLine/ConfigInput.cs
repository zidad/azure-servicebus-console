using JasperFx.CommandLine;

namespace ServiceBusConsole.CommandLine;

public sealed class ConfigInput
{
    [Description("get, set, unset, list or path")]
    public ConfigAction Action { get; set; }

    [Description("Dotted key, such as ui.theme")]
    public string? Key { get; set; }

    [Description("Value to set")]
    public string? Value { get; set; }

    [Description("With list: also the known keys that are unset, with their default and description")]
    public bool AllFlag { get; set; }

    [IgnoreOnCommandLine]
    public ConfigFile File { get; set; } = new(AppPaths.ConfigFile);

    /// <summary>The keys <c>set</c> accepts without a warning and <c>list --all</c> shows.</summary>
    [IgnoreOnCommandLine]
    public IReadOnlyList<ConfigKey> Keys { get; set; } = ConfigKeys.App;

    [IgnoreOnCommandLine]
    public TextWriter Out { get; set; } = Console.Out;

    [IgnoreOnCommandLine]
    public TextWriter Error { get; set; } = Console.Error;
}
