namespace ServiceBusConsole.Configuration;

/// <summary>The settings azsb knows. Keys under <see cref="PluginPrefix"/> belong to plugins and are never unknown.</summary>
public static class ConfigKeys
{
    public const string PluginPrefix = "plugins.";

    public static ConfigKey Theme { get; } =
        new("ui.theme", ConfigurationStrings.ConfigKeys_Theme, "auto");

    // Described in the transport's words, so read after Terms.Use.
    public static ConfigKey Protect =>
        new("cli.protect", ConfigurationStrings.FormatConfigKeys_Protect(Terms.Broker.Many, Terms.Broker.OneInText));

    public static ConfigKey RedactNamespaces =>
        new("cli.redact.namespaces", ConfigurationStrings.FormatConfigKeys_RedactBrokers(Terms.Broker.Many));

    public static ConfigKey RedactFields =>
        new("cli.redact.fields", ConfigurationStrings.FormatConfigKeys_RedactFields(Terms.Broker.ManyInText));

    public static IReadOnlyList<ConfigKey> App => [Theme, Protect, RedactNamespaces, RedactFields];

    /// <summary>The app's keys and <paramref name="extra"/> ones, such as the transport's and a plugin's.</summary>
    public static IReadOnlyList<ConfigKey> With(IEnumerable<ConfigKey> extra) => [.. App, .. extra];

    public static bool IsSecret(string key, IEnumerable<ConfigKey> keys) =>
        keys.Any(k => k.IsSecret && string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));

    public static bool IsKnown(string key, IEnumerable<ConfigKey> keys) =>
        key.StartsWith(PluginPrefix, StringComparison.OrdinalIgnoreCase)
        || keys.Any(k => string.Equals(k.Key, key, StringComparison.OrdinalIgnoreCase));
}
