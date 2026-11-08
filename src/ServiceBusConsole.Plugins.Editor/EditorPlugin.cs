using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Plugins.Editor;

public sealed class EditorPlugin : IPlugin
{
    public string Id => OpenInEditorAction.Plugin;

    public string Name => "Editor";

    public string Description => "Opens a message as a JSON file in your editor";

    public IReadOnlyList<PluginSetting> Settings { get; } =
    [
        new(OpenInEditorAction.Command, "Editor executable, found on PATH (Windows: code.cmd)", "code"),
        new(OpenInEditorAction.Arguments, $"Arguments, split on spaces; {OpenInEditorAction.FilePlaceholder} is the message file, appended when absent"),
        new(OpenInEditorAction.Directory, "Folder for the message files instead of the state directory's messages folder"),
    ];

    public void ConfigureServices(IServiceCollection services) => services.AddSingleton<IMessageAction, OpenInEditorAction>();

    public string? Missing(IConfiguration settings, PluginEnvironment environment) =>
        environment.MissingCommand(settings[OpenInEditorAction.Command]);
}
