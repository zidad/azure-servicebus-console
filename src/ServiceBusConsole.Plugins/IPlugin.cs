using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins;

/// <summary>
/// A bundle of features the console loads when it is enabled: <c>plugins.&lt;id&gt;.enabled</c>, or while that is unset,
/// when nothing it needs is <see cref="Missing"/>.
/// </summary>
public interface IPlugin
{
    /// <summary>Lower case letters, digits and dashes; settings are <c>plugins.&lt;id&gt;.&lt;key&gt;</c>.</summary>
    string Id { get; }

    string Name { get; }

    string Description { get; }

    IReadOnlyList<PluginSetting> Settings { get; }

    /// <summary>Registers the plugin's <see cref="IMessageAction"/>s and what they need; called only when enabled.</summary>
    void ConfigureServices(IServiceCollection services);

    /// <summary>What the plugin needs and can't find, e.g. <c>gh not found</c>; null when it can run.</summary>
    /// <param name="settings">The plugin's settings, over their defaults.</param>
    string? Missing(IConfiguration settings, PluginEnvironment environment) => null;
}
