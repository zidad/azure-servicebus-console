using Microsoft.Extensions.Configuration;
using ServiceBusConsole.Plugins.Editor.Resources;

namespace ServiceBusConsole.Plugins.Editor;

public sealed class EditorPlugin : IPlugin
{
    public string Id => OpenInEditorAction.Plugin;

    public string Name => Strings.EditorPlugin_Name;

    public string Description => Strings.EditorPlugin_Description;

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(OpenInEditorAction.Command, Strings.EditorPlugin_CommandSetting, "code"),
        new(OpenInEditorAction.Arguments, Strings.FormatEditorPlugin_ArgumentsSetting(OpenInEditorAction.FilePlaceholder)),
        new(OpenInEditorAction.Directory, Strings.EditorPlugin_DirectorySetting),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, OpenInEditorAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[OpenInEditorAction.Command]);
}
