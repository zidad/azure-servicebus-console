using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.Plugins.Claude.Resources;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>Opens Claude Code in a new terminal window, in the codebase that sends or handles a message, primed with it.</summary>
public sealed class ClaudePlugin : IPlugin
{
    public string Id => "claude";

    public string Name => Strings.ClaudePlugin_Name;

    public string Description => Strings.ClaudePlugin_Description;

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(ClaudeSettings.CommandKey, Strings.ClaudePlugin_CommandSetting, "claude"),
        new(ClaudeSettings.WorkingDirectoryKey, Strings.ClaudePlugin_WorkingDirectorySetting),
        new(ClaudeSettings.AllowedToolsKey, Strings.ClaudePlugin_AllowedToolsSetting, "Read,Grep,Glob"),
        new(ClaudeSettings.ModelKey, Strings.ClaudePlugin_ModelSetting),
        new(ClaudeSettings.PromptKey, Strings.ClaudePlugin_PromptSetting),
        new(ClaudeSettings.TerminalKey, Strings.ClaudePlugin_TerminalSetting),
    ];

    public void ConfigureServices(IServiceCollection services) =>
        services.AddSingleton<IMessageAction>(new AnalyseMessageAction(Id));

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[ClaudeSettings.CommandKey]);
}
