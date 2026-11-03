using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Plugins.Claude;

/// <summary>The plugin's settings, read when an analysis starts.</summary>
public sealed record ClaudeSettings
{
    public const string CommandKey = "command";
    public const string WorkingDirectoryKey = "workingDirectory";
    public const string AllowedToolsKey = "allowedTools";
    public const string ModelKey = "model";
    public const string PromptKey = "prompt";
    public const string TerminalKey = "terminal";

    public required string Command { get; init; }
    public required string WorkingDirectory { get; init; }
    public string? AllowedTools { get; init; }
    public string? Model { get; init; }
    public string? Prompt { get; init; }
    public string? Terminal { get; init; }

    public static ClaudeSettings From(MessageActionContext context) => new()
    {
        Command = Value(context.Settings, CommandKey) ?? "claude",
        WorkingDirectory = ExistingDirectory(context, Value(context.Settings, WorkingDirectoryKey) ?? Environment.CurrentDirectory),
        AllowedTools = Value(context.Settings, AllowedToolsKey),
        Model = Value(context.Settings, ModelKey),
        Prompt = Value(context.Settings, PromptKey),
        Terminal = Value(context.Settings, TerminalKey),
    };

    private static string? Value(IConfiguration settings, string key) =>
        settings[key]?.Trim() is { Length: > 0 } value ? value : null;

    private static string ExistingDirectory(MessageActionContext context, string path) =>
        Path.GetFullPath(ExpandHome(path)) is var full && Directory.Exists(full)
            ? full
            : throw new InvalidOperationException(
                $"Working directory {full} does not exist: azsb config set plugins.{context.PluginId}.{WorkingDirectoryKey} <path to your code>");

    private static string ExpandHome(string path) =>
        path == "~" || path.StartsWith("~/", StringComparison.Ordinal)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), path[1..].TrimStart('/'))
            : path;
}
