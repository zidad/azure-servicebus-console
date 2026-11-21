namespace ServiceBusConsole.Configuration;

/// <summary>
/// An app built on this UI: its command name, what it does, and where it keeps its files, by the XDG base directory
/// conventions (Windows: the AppData folders).
/// </summary>
/// <param name="Name">The command, lower case: <c>azsb</c>. Also names its folders and environment variables.</param>
/// <param name="Description">The root command's help line.</param>
public sealed record AppIdentity(string Name, string Description)
{
    /// <summary><c>AZSB_</c>: environment variables with it override the config file (<c>AZSB_UI__THEME</c>).</summary>
    public string EnvironmentPrefix => $"{Name.ToUpperInvariant()}_";

    /// <summary><c>$AZSB_CONFIG</c>, else <c>config.json</c> in the app's config directory.</summary>
    public string ConfigFile =>
        Variable($"{EnvironmentPrefix}CONFIG")
        ?? Path.Combine(
            Variable("XDG_CONFIG_HOME")
            ?? (OperatingSystem.IsWindows()
                ? Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData)
                : Path.Combine(Home, ".config")),
            Name, "config.json");

    /// <summary>For files the app writes and keeps between sessions.</summary>
    public string StateDirectory =>
        Variable("XDG_STATE_HOME") is { } state
            ? Path.Combine(state, Name)
            : OperatingSystem.IsWindows()
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), Name, "state")
                : Path.Combine(Home, ".local", "state", Name);

    private static string Home => Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

    private static string? Variable(string name) =>
        Environment.GetEnvironmentVariable(name) is { Length: > 0 } value ? value : null;
}
