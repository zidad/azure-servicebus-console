namespace ServiceBusConsole.CommandLine;

public sealed record ConfigInput : CommandInput
{
    public ConfigAction Action { get; init; }

    /// <summary>Dotted key, such as <c>ui.theme</c>.</summary>
    public string? Key { get; init; }

    public string? Value { get; init; }

    /// <summary><c>--all</c>: with list, also the known keys that are unset.</summary>
    public bool All { get; init; }

    public ConfigFile File { get; init; } = new(AppPaths.ConfigFile);

    /// <summary>The keys <c>set</c> accepts without a warning and <c>list --all</c> shows.</summary>
    public IReadOnlyList<ConfigKey> Keys { get; init; } = ConfigKeys.App;

    public TextWriter Out { get; init; } = Console.Out;

    public TextWriter Error { get; init; } = Console.Error;
}
