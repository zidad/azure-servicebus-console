namespace ServiceBusConsole.Plugins.Claude;

/// <summary>
/// The script the terminal window runs: an interactive Claude Code session in the codebase, the prompt as its first message.
/// </summary>
public static class ClaudeCommand
{
    /// <summary>Linux takes at most 128 KiB in one argument; a longer prompt is read from its file instead.</summary>
    public const int MaxInlinePrompt = 100_000;

    public static IReadOnlyList<string> Options(ClaudeSettings settings, AnalysisRun run) =>
    [
        .. settings.Model is { } model ? ["--model", model] : Array.Empty<string>(),
        .. settings.AllowedTools is { } tools ? ["--allowedTools", tools] : Array.Empty<string>(),
        "--add-dir", run.Directory,
    ];

    public static string ReadPrompt(AnalysisRun run) => $"Read {run.PromptFile} and follow its instructions.";

    // cmd can't pass a multi-line argument, so Windows always points at the file.
    public static string Script(ClaudeSettings settings, AnalysisRun run, string claude, string prompt) =>
        run.Os == ClaudeOs.Windows
            ? string.Join("\r\n",
                "@echo off",
                $"cd /d {Cmd(settings.WorkingDirectory)}",
                // Started from a Claude Code session, the new claude would otherwise take itself for part of it.
                "set CLAUDECODE=",
                string.Join(' ', [Cmd(claude), .. Options(settings, run).Select(Cmd), Cmd(ReadPrompt(run))]),
                "")
            : string.Join("\n",
                "#!/bin/sh",
                $"cd {Sh(settings.WorkingDirectory)} || exit 1",
                "unset CLAUDECODE",
                string.Join(' ',
                [
                    "exec", Sh(claude), .. Options(settings, run).Select(Sh),
                    prompt.Length <= MaxInlinePrompt ? $"\"$(cat {Sh(run.PromptFile)})\"" : Sh(ReadPrompt(run)),
                ]),
                "");

    private static string Sh(string value) => $"'{value.Replace("'", "'\\''")}'";

    private static string Cmd(string value) => $"\"{value.Replace("\"", "\"\"")}\"";
}
