using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.PluginHost;

/// <summary>The message actions of the enabled plugins, and the context they run in.</summary>
public sealed class PluginActions(IEnumerable<IMessageAction> actions, PluginCatalog catalog, IConfiguration configuration, IPluginHost host)
{
    private readonly IReadOnlyList<OfferedAction> _all =
        [.. actions.SelectMany(a => catalog.Find(a.PluginId) is { } plugin ? [new OfferedAction(plugin, a)] : Array.Empty<OfferedAction>())];

    /// <summary>The actions that can run on <paramref name="message"/>, by plugin, then in registration order.</summary>
    public IReadOnlyList<OfferedAction> For(MessageInfo message, MessageSource source, SessionSnapshot session) =>
        [.. _all.Where(a => CanRun(a, Context(a.Plugin, message, source, session, CallbackProgress.None))).OrderBy(a => a.Plugin.Name, StringComparer.OrdinalIgnoreCase)];

    /// <summary>Whether any action can run; takes the snapshot only when there are actions.</summary>
    public bool Any(MessageInfo message, MessageSource source, Func<SessionSnapshot> session) =>
        _all.Count > 0 && For(message, source, session()).Count > 0;

    public MessageActionContext Context(IPlugin plugin, MessageInfo message, MessageSource source, SessionSnapshot session, IProgress<string> progress) => new()
    {
        PluginId = plugin.Id,
        Message = message,
        Source = source,
        Session = session,
        Settings = PluginCatalog.Settings(plugin, configuration),
        Host = host,
        Progress = progress,
    };

    // A plugin bug in CanRun hides its action instead of breaking the page.
    private static bool CanRun(OfferedAction offered, MessageActionContext context)
    {
        try { return offered.Action.CanRun(context); }
        catch (Exception) { return false; }
    }
}
