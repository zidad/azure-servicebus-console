using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>Opens Claude Code in a new terminal window, in the codebase that sends or handles a message, primed with it.</summary>
public sealed class ClaudePlugin : IPlugin
{
    public string Id => "claude";

    public string Name => "Claude Code";

    public string Description => "Opens Claude Code in a new terminal window in your codebase to analyse a message (uses your Claude Code login)";

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(ClaudeSettings.CommandKey, "Claude Code executable", "claude"),
        new(ClaudeSettings.WorkingDirectoryKey, "Codebase Claude Code runs in (unset: the directory azsb was started in)"),
        new(ClaudeSettings.AllowedToolsKey, "Tools Claude Code may use without asking, comma separated (empty: it asks for each)", "Read,Grep,Glob"),
        new(ClaudeSettings.ModelKey, "Model, e.g. sonnet or opus (unset: Claude Code's)"),
        new(ClaudeSettings.PromptKey, "First message; placeholders {message} and {context} (Markdown), {messageFile}, {contextFile}, {entity}, {subject}, {sequenceNumber}, ..."),
        new(ClaudeSettings.TerminalKey, "Command that opens a window running {script} in {dir} (unset: tmux window in tmux, else Terminal on macOS, Windows Terminal or cmd, x-terminal-emulator)"),
    ];

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IMessageAction>(new AnalyseMessageAction(Id));

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[ClaudeSettings.CommandKey]);
}
