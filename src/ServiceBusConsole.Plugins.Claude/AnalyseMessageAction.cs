using System.ComponentModel;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>
/// Writes the message, the session context and a prompt with both to a run folder, then opens a terminal window with
/// an interactive Claude Code session in the configured codebase, the prompt as its first message.
/// </summary>
public sealed class AnalyseMessageAction(string pluginId, ClaudePlatform platform) : IMessageAction
{
    public AnalyseMessageAction(string pluginId) : this(pluginId, ClaudePlatform.Current) { }

    public string PluginId { get; } = pluginId;

    public string Id => "analyse";

    public string Label => "Analyse with Claude Code";

    public string Icon => "🤖";

    public bool CanRun(MessageActionContext context) => true;

    public async Task<ActionResult> RunAsync(MessageActionContext context, CancellationToken ct)
    {
        var settings = ClaudeSettings.From(context);
        var run = AnalysisRun.Create(context, platform.Os);
        // The window's shell may not have azsb's PATH (macOS Terminal starts a fresh login).
        var claude = platform.FindCommand(settings.Command) ?? settings.Command;
        var prompt = ClaudePrompt.For(context, settings.Prompt, run);
        await run.WriteAsync(context, prompt, ClaudeCommand.Script(settings, run, claude, prompt), ct);

        var terminal = TerminalCommand.For(settings.Terminal, platform, run.ScriptFile, settings.WorkingDirectory);
        context.Progress.Report($"Opening Claude Code in {settings.WorkingDirectory}…");
        try
        {
            (await context.Host.RunProcessAsync(terminal, ct)).EnsureSuccess(terminal.FileName);
        }
        catch (InvalidOperationException e) when (e.InnerException is Win32Exception)
        {
            throw new InvalidOperationException(
                $"Could not open a terminal with {terminal.FileName}: azsb config set plugins.{PluginId}.{ClaudeSettings.TerminalKey} \"<command> {TerminalCommand.ScriptPlaceholder}\"", e);
        }
        return new ActionResult($"Claude Code opened in a new window, in {settings.WorkingDirectory}", FilePath: run.PromptFile);
    }
}
