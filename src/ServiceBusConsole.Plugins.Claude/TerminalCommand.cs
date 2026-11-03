namespace ServiceBusConsole.Plugins.Claude;

/// <summary>The command that opens a window running the run's script: the <c>terminal</c> setting, else one per platform.</summary>
public static class TerminalCommand
{
    public const string ScriptPlaceholder = "{script}";
    public const string DirectoryPlaceholder = "{dir}";

    public static ProcessSpec For(string? setting, ClaudePlatform platform, string script, string directory)
    {
        var template = setting?.Split(' ', StringSplitOptions.RemoveEmptyEntries) ?? Default(platform);
        string[] parts = template.Contains(ScriptPlaceholder) ? template : [.. template, ScriptPlaceholder];
        return new ProcessSpec(parts[0], [.. parts.Skip(1).Select(p => p.Replace(ScriptPlaceholder, script).Replace(DirectoryPlaceholder, directory))])
        {
            WorkingDirectory = directory,
        };
    }

    // Each returns once the window is open, so the action doesn't wait for the session.
    public static string[] Default(ClaudePlatform platform) => platform switch
    {
        _ when platform.Variable("TMUX") is { Length: > 0 } => ["tmux", "new-window", "-c", DirectoryPlaceholder, ScriptPlaceholder],
        { Os: ClaudeOs.Windows } when platform.Variable("WT_SESSION") is { Length: > 0 } =>
            ["wt.exe", "-w", "0", "new-tab", "-d", DirectoryPlaceholder, "cmd.exe", "/k", ScriptPlaceholder],
        { Os: ClaudeOs.Windows } => ["cmd.exe", "/c", "start", "Claude Code", "/D", DirectoryPlaceholder, "cmd.exe", "/k", ScriptPlaceholder],
        { Os: ClaudeOs.MacOS } => ["open", "-a", "Terminal", ScriptPlaceholder],
        _ => ["setsid", "-f", "x-terminal-emulator", "-e", ScriptPlaceholder],
    };
}
