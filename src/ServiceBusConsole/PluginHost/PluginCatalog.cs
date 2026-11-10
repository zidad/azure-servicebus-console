using System.Text.RegularExpressions;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.PluginHost;

/// <summary>The bundled plugins, with their settings and whether each is enabled.</summary>
public sealed partial class PluginCatalog
{
    public const string AssemblyPrefix = "ServiceBusConsole.Plugins.";

    /// <summary><c>enabled</c> when unset, or set to anything but true or false.</summary>
    public const string AutoEnabled = "auto";

    public PluginCatalog(IEnumerable<IPlugin> plugins, PluginEnvironment? environment = null)
    {
        Environment = environment ?? PluginEnvironment.Current;
        Plugins = [.. plugins.OrderBy(p => p.Id, StringComparer.Ordinal)];
        if (Plugins.FirstOrDefault(p => !IdPattern().IsMatch(p.Id)) is { } invalid)
            throw new InvalidOperationException($"Plugin {invalid.GetType().FullName} has id '{invalid.Id}': use lower case letters, digits and dashes.");
        if (Plugins.GroupBy(p => p.Id).FirstOrDefault(g => g.Count() > 1) is { } duplicate)
            throw new InvalidOperationException($"More than one plugin has id '{duplicate.Key}'.");
    }

    public static PluginCatalog Empty { get; } = new([]);

    public IReadOnlyList<IPlugin> Plugins { get; }

    /// <summary>Where <see cref="IPlugin.Missing"/> looks for what plugins need.</summary>
    public PluginEnvironment Environment { get; }

    public IPlugin? Find(string id) => Plugins.FirstOrDefault(p => p.Id == id);

    /// <summary><c>plugins.&lt;id&gt;.enabled</c> when true or false, else on unless the plugin misses something.</summary>
    public PluginStatus Status(IPlugin plugin, IConfiguration configuration) =>
        bool.TryParse(configuration[$"plugins:{plugin.Id}:enabled"], out var enabled)
            ? new(enabled, null)
            : plugin.Missing(Settings(plugin, configuration), Environment) is { } missing
                ? new(false, missing)
                : new(true, null);

    public IEnumerable<IPlugin> Enabled(IConfiguration configuration) => Plugins.Where(p => Status(p, configuration).Enabled);

    /// <summary>Each plugin's <c>enabled</c> key and settings, for <c>azsb config</c>.</summary>
    public IReadOnlyList<ConfigKey> ConfigKeys => [.. Plugins.SelectMany(KeysOf)];

    public static IEnumerable<ConfigKey> KeysOf(IPlugin plugin) =>
    [
        new(EnabledKey(plugin), PluginHostStrings.FormatPluginCatalog_EnabledKey(plugin.Name), AutoEnabled),
        .. plugin.Settings.Select(s => new ConfigKey(Key(plugin, s), s.Description, s.Default) { IsSecret = s.IsSecret }),
    ];

    public static string EnabledKey(IPlugin plugin) => $"{Configuration.ConfigKeys.PluginPrefix}{plugin.Id}.enabled";

    public static string Key(IPlugin plugin, PluginSetting setting) => $"{Configuration.ConfigKeys.PluginPrefix}{plugin.Id}.{setting.Key}";

    /// <summary>The plugin's section over its settings' defaults.</summary>
    public static IConfiguration Settings(IPlugin plugin, IConfiguration configuration) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(plugin.Settings
                .Where(s => s.Default is not null)
                .Select(s => KeyValuePair.Create(ConfigurationPath.Combine(s.Key.Split('.')), s.Default)))
            .AddInMemoryCollection(configuration.GetSection($"plugins:{plugin.Id}").AsEnumerable(makePathsRelative: true))
            .Build();

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex IdPattern();
}
