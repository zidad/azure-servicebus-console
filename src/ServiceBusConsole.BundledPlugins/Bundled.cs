using ServiceBusConsole.Plugins;
using ServiceBusConsole.Plugins.Claude;
using ServiceBusConsole.Plugins.Editor;
using ServiceBusConsole.Plugins.GitHub;
using ServiceBusConsole.Plugins.Linear;
using ServiceBusConsole.Plugins.Trace;

namespace ServiceBusConsole.BundledPlugins;

/// <summary>
/// The plugins that ship with azsb, listed rather than discovered: a Native AOT binary cannot load assemblies.
/// Its own project, so the UI references no plugin.
/// </summary>
public static class Bundled
{
    public static IReadOnlyList<IPlugin> Plugins => [new ClaudePlugin(), new EditorPlugin(), new GitHubPlugin(), new LinearPlugin(), new TracePlugin()];
}
