namespace ServiceBusConsole.Plugins;

/// <summary>Finds an executable like a shell does: a path as given, else a name in the <see cref="SearchPath"/> folders.</summary>
public static class CommandSearch
{
    /// <summary>
    /// <c>PATH</c>, then the usual install folders it lacks. Apps started outside a terminal (an IDE, the Dock) get a
    /// minimal <c>PATH</c> without Homebrew's or the user's own bin folder.
    /// </summary>
    public static string SearchPath(string? path, string home) =>
        string.Join(Path.PathSeparator, (path ?? "")
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Concat(OperatingSystem.IsWindows() ? [] : ["/opt/homebrew/bin", "/usr/local/bin", Path.Combine(home, ".local", "bin")])
            .Distinct());

    public static string SearchPath() =>
        SearchPath(Environment.GetEnvironmentVariable("PATH"), Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));

    public static string? Find(string command, string? path = null, string? extensions = null)
    {
        if (string.IsNullOrWhiteSpace(command)) return null;
        if (command.Contains(Path.DirectorySeparatorChar) || command.Contains(Path.AltDirectorySeparatorChar))
            return Candidates(command, extensions).FirstOrDefault(IsExecutable);
        return (path ?? SearchPath())
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .SelectMany(folder => Candidates(Path.Combine(folder, command), extensions))
            .FirstOrDefault(IsExecutable);
    }

    // Windows runs "code" as code.cmd or code.exe, by PATHEXT.
    private static IEnumerable<string> Candidates(string file, string? extensions) =>
        OperatingSystem.IsWindows()
            ? [file, .. (extensions ?? Environment.GetEnvironmentVariable("PATHEXT") ?? ".COM;.EXE;.BAT;.CMD")
                .Split(';', StringSplitOptions.RemoveEmptyEntries)
                .Select(extension => file + extension)]
            : [file];

    private static bool IsExecutable(string file) =>
        File.Exists(file)
        && (OperatingSystem.IsWindows()
            || (File.GetUnixFileMode(file) & (UnixFileMode.UserExecute | UnixFileMode.GroupExecute | UnixFileMode.OtherExecute)) != 0);
}
