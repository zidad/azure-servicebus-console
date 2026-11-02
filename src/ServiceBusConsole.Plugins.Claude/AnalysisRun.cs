using System.Globalization;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>A run's folder, <c>plugins/claude/&lt;entity&gt;-&lt;seq&gt;-&lt;utc time&gt;/</c>, and the files in it.</summary>
public sealed record AnalysisRun(string Directory, ClaudeOs Os)
{
    public string MessageFile => Path.Combine(Directory, "message.json");
    public string ContextFile => Path.Combine(Directory, "context.json");
    public string PromptFile => Path.Combine(Directory, "prompt.md");
    public string ScriptFile => Path.Combine(Directory, Os == ClaudeOs.Windows ? "start.cmd" : "start.sh");

    public static AnalysisRun Create(MessageActionContext context, ClaudeOs os) =>
        new(System.IO.Directory.CreateDirectory(Path.Combine(PluginFiles.PluginDirectory(context), Name(context))).FullName, os);

    public async Task WriteAsync(MessageActionContext context, string prompt, string script, CancellationToken ct)
    {
        await File.WriteAllTextAsync(MessageFile, MessageJson.Format(context.Message), ct);
        await File.WriteAllTextAsync(ContextFile, SessionJson.Format(context.Session), ct);
        await File.WriteAllTextAsync(PromptFile, prompt, ct);
        await File.WriteAllTextAsync(ScriptFile, script, ct);
        if (!OperatingSystem.IsWindows())
            File.SetUnixFileMode(ScriptFile, File.GetUnixFileMode(ScriptFile) | UnixFileMode.UserExecute);
    }

    private static string Name(MessageActionContext context) =>
        $"{Path.GetFileNameWithoutExtension(PluginFiles.MessageFileName(context, "json"))}-" +
        context.Session.At.UtcDateTime.ToString("yyyyMMdd-HHmmssfff", CultureInfo.InvariantCulture);
}
