using System.Globalization;
using System.Text.RegularExpressions;

namespace ServiceBusConsole.Plugins;

/// <summary>
/// Where plugins write, under <see cref="IPluginHost.StateDirectory"/>: message exports in the shared <c>messages</c> folder,
/// anything else in <c>plugins/&lt;id&gt;</c>. Folders are created when asked for.
/// </summary>
public static partial class PluginFiles
{
    /// <summary><c>orders-dlq-42.json</c>: the entity and sequence number, safe as a file name.</summary>
    /// <param name="context">The message.</param>
    /// <param name="extension">With or without the dot.</param>
    public static string MessageFileName(MessageActionContext context, string extension) =>
        $"{Safe(context.Source.DisplayName)}-{context.Message.SequenceNumber.ToString(CultureInfo.InvariantCulture)}.{extension.TrimStart('.')}";

    /// <summary>The full path of <see cref="MessageFileName"/> in the shared <c>messages</c> folder.</summary>
    public static string MessagePath(MessageActionContext context, string extension) =>
        Path.Combine(Created(Path.Combine(context.Host.StateDirectory, "messages")), MessageFileName(context, extension));

    /// <summary>The plugin's own folder.</summary>
    public static string PluginDirectory(MessageActionContext context) =>
        Created(Path.Combine(context.Host.StateDirectory, "plugins", context.PluginId));

    private static string Created(string directory) => Directory.CreateDirectory(directory).FullName;

    private static string Safe(string name) => Unsafe().Replace(name.ToLowerInvariant(), "-").Trim('-', '.');

    [GeneratedRegex("[^a-z0-9_]+")]
    private static partial Regex Unsafe();
}
