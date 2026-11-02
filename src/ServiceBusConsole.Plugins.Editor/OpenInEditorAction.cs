using System.ComponentModel;

namespace ServiceBusConsole.Plugins.Editor;

/// <summary>Writes the message as JSON, one file per message (reopening overwrites it), and opens it in the editor.</summary>
public sealed class OpenInEditorAction : IMessageAction
{
    public const string Plugin = "editor";
    public const string Command = "command";
    public const string Arguments = "arguments";
    public const string Directory = "directory";
    public const string FilePlaceholder = "{file}";

    public string PluginId => Plugin;

    public string Id => "open";

    public string Label => "Open in editor";

    public string Icon => "📝";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var file = FilePath(context);
        await File.WriteAllTextAsync(file, MessageJson.Format(context.Message) + "\n", ct);

        var spec = Process(context, file);
        context.Progress.Report($"Opening in {spec.FileName}…");
        try
        {
            (await context.Host.RunProcessAsync(spec, ct)).EnsureSuccess(spec.FileName);
        }
        catch (Exception e) when (e is Win32Exception || e.InnerException is Win32Exception)
        {
            throw new InvalidOperationException(
                $"Could not start {spec.FileName}; set your editor: azsb config set plugins.{Plugin}.{Command} <cmd>", e);
        }
        return new ActionResult($"Opened in {spec.FileName}", FilePath: file);
    }

    public static string FilePath(MessageActionContext context) =>
        context.Settings[Directory] is { Length: > 0 } directory
            ? Path.Combine(System.IO.Directory.CreateDirectory(directory).FullName, PluginFiles.MessageFileName(context, "json"))
            : PluginFiles.MessagePath(context, "json");

    public static ProcessSpec Process(MessageActionContext context, string file) =>
        new(context.RequiredSetting(Command), ArgumentList(context.Settings[Arguments], file));

    private static string[] ArgumentList(string? arguments, string file) =>
        (arguments ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries) is var parts
        && parts.Any(part => part.Contains(FilePlaceholder, StringComparison.Ordinal))
            ? [.. parts.Select(part => part.Replace(FilePlaceholder, file, StringComparison.Ordinal))]
            : [.. parts, file];
}
