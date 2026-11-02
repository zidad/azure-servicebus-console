namespace ServiceBusConsole.Plugins;

/// <summary>What a plugin can check for on this machine, to decide whether it is on by default.</summary>
public sealed record PluginEnvironment(Func<string, bool> CommandExists, Func<string, string?> Variable)
{
    public static PluginEnvironment Current { get; } = new(command => CommandSearch.Find(command) is not null, Environment.GetEnvironmentVariable);

    /// <summary>For <see cref="IPlugin.Missing"/>: null when <paramref name="command"/> is on the <c>PATH</c> or an executable's path.</summary>
    public string? MissingCommand(string? command) => command switch
    {
        null or "" => "no command set",
        _ when CommandExists(command) => null,
        _ => $"{command} not found",
    };
}
