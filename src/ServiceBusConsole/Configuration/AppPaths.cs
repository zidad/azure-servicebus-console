namespace ServiceBusConsole.Configuration;

/// <summary>Where azsb keeps its files, by the XDG base directory conventions (Windows: the AppData folders).</summary>
public static class AppPaths
{
    /// <summary><c>$AZSB_CONFIG</c>, else <c>config.json</c> in the azsb config directory.</summary>
    public static string ConfigFile =>
        Variable("AZSB_CONFIG")
        ?? Path.Combine(
            Variable("XDG_CONFIG_HOME")
            ?? (OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : Path.Combine(Home, ".config")),
            "azsb", "config.json");

    /// <summary>For files azsb writes and keeps between sessions.</summary>
    public static string StateDirectory =>
        Variable("XDG_STATE_HOME") is { } state
            ? Path.Combine(state, "azsb")
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "azsb", "state")
                : Path.Combine(Home, ".local", "state", "azsb");

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string? Variable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
