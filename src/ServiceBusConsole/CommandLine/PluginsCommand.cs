namespace ServiceBusConsole.CommandLine;

/// <summary>Lists the plugins, whether they are enabled, and their settings.</summary>
public static class PluginsCommand
{
    public static bool Execute(PluginsInput input)
    {
        IEnumerable<string> lines = input.Catalog.Plugins.Count == 0
            ? [CommandLineStrings.PluginsCommand_NoPlugins]
            : input.Catalog.Plugins.SelectMany((p, i) => i == 0 ? Describe(p, input) : Describe(p, input).Prepend(""));
        foreach (var line in lines) input.Out.WriteLine(line);
        return true;
    }

    private static IEnumerable<string> Describe(IPlugin plugin, PluginsInput input) =>
    [
        $"{plugin.Id}  {plugin.Name}  {input.Catalog.Status(plugin, input.Configuration)}",
        $"  {plugin.Description}",
        .. PluginCatalog.KeysOf(plugin).Select(key => CommandLineStrings.FormatPluginsCommand_Setting(
            key.Key, Value(key, input), key.Default ?? CommandLineStrings.PluginsCommand_NoDefault, key.Description)),
    ];

    private static string Value(ConfigKey key, PluginsInput input) => input.Configuration[key.ConfigurationPath] switch
    {
        null or "" => "",
        _ when key.IsSecret => ConfigKey.Mask,
        var value => value,
    };
}
