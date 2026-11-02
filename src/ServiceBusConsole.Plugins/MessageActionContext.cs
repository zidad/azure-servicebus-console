using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Plugins;

/// <summary>The message an action runs on, where it came from, and what the action may use.</summary>
public sealed record MessageActionContext
{
    public required string PluginId { get; init; }
    public required MessageInfo Message { get; init; }
    public required MessageSource Source { get; init; }

    /// <summary>What the user is looking at and the session's recent activity, taken when the action starts.</summary>
    public required SessionSnapshot Session { get; init; }

    /// <summary>The plugin's <c>plugins:&lt;id&gt;</c> section over its <see cref="PluginSetting.Default"/>s.</summary>
    public required IConfiguration Settings { get; init; }

    public required IPluginHost Host { get; init; }

    /// <summary>Status text shown while the action runs; may be reported from any thread.</summary>
    public required IProgress<string> Progress { get; init; }

    /// <summary>The setting's value; when it is empty, throws a message that tells the user how to set it.</summary>
    public string RequiredSetting(string key) =>
        Settings[key] is { Length: > 0 } value
            ? value
            : throw new InvalidOperationException($"Set plugins.{PluginId}.{key} first: azsb config set plugins.{PluginId}.{key} <value>");
}
