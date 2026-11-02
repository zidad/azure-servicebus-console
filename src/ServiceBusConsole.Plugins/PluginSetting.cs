namespace ServiceBusConsole.Plugins;

/// <summary>A setting of a plugin, <c>plugins.&lt;id&gt;.&lt;Key&gt;</c>; <see cref="IsSecret"/> values are masked when listed.</summary>
/// <param name="Key">Relative to the plugin's section, e.g. <c>repo</c>.</param>
public sealed record PluginSetting(string Key, string Description, string? Default = null, bool IsSecret = false);
