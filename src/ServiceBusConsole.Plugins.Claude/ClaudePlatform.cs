namespace ServiceBusConsole.Plugins.Claude;

/// <summary>The machine the terminal window opens on; tests pass their own.</summary>
public sealed record ClaudePlatform(ClaudeOs Os, Func<string, string?> Variable, Func<string, string?> FindCommand)
{
    public static ClaudePlatform Current { get; } = new(
        OperatingSystem.IsWindows() ? ClaudeOs.Windows : OperatingSystem.IsMacOS() ? ClaudeOs.MacOS : ClaudeOs.Linux,
        Environment.GetEnvironmentVariable,
        command => CommandSearch.Find(command));
}

public enum ClaudeOs { Linux, MacOS, Windows }
