namespace ServiceBusConsole.Configuration;

/// <summary>The settings azsb knows. Keys under <see cref="PluginPrefix"/> belong to plugins and are never unknown.</summary>
public static class ConfigKeys
{
    public const string PluginPrefix = "plugins.";

    public static ConfigKey Theme { get; } =
        new("ui.theme", "Colors for a dark or light terminal background: auto (COLORFGBG, else dark), dark or light", "auto");

    public static IReadOnlyList<ConfigKey> App { get; } = [Theme];

    /// <summary>The app's keys and <paramref name="extra"/> ones, such as a plugin's.</summary>
    public static IReadOnlyList<ConfigKey> With(IEnumerable<ConfigKey> extra) => [.. App, .. extra];

    public static bool IsSecret(string key, IEnumerable<ConfigKey> keys) =>
        keys.Any(k => k.IsSecret && string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnown(string key, IEnumerable<ConfigKey> keys) =>
        key.StartsWith(PluginPrefix, StringComparison.OrdinalIgnoreCase)
        || keys.Any(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
}
