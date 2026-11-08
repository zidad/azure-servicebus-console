using System.Text.Json;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal static class EmulatorConfigReader
{
    /// <summary>The first namespace in the nearest <c>emulator/Config.json</c>, if any: see <see cref="Find"/>.</summary>
    public static EmulatorNamespaceDef? TryLoad() =>
        Find(Directory.GetCurrentDirectory(), AppContext.BaseDirectory) is { } path ? Parse(File.ReadAllText(path)) : null;

    /// <summary>
    /// <c>emulator/Config.json</c> in <paramref name="workingDirectory"/> or a parent, else in the app's folder or a parent:
    /// <c>dotnet run --project</c> from another folder still finds the repository's.
    /// </summary>
    public static string? Find(string workingDirectory, string appDirectory) =>
        new[] { workingDirectory, appDirectory }
            .SelectMany(start => Ancestors(new DirectoryInfo(start)))
            .Select(directory => Path.Combine(directory.FullName, "emulator", "Config.json"))
            .FirstOrDefault(File.Exists);

    private static IEnumerable<DirectoryInfo> Ancestors(DirectoryInfo? directory)
    {
        for (; directory is not null; directory = directory.Parent) yield return directory;
    }

    public static EmulatorNamespaceDef? Parse(string json) =>
        JsonSerializer.Deserialize(json, EmulatorJsonContext.Default.EmulatorConfigFile)?.UserConfig.Namespaces.FirstOrDefault();
}
