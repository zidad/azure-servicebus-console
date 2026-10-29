using System.Text.Json;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal static class EmulatorConfigReader
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    /// <summary>The first namespace in <c>emulator/Config.json</c> under the working directory, if any.</summary>
    public static EmulatorNamespace? TryLoad() =>
        Path.Combine(Directory.GetCurrentDirectory(), "emulator", "Config.json") is var path && File.Exists(path)
            ? Parse(File.ReadAllText(path))
            : null;

    public static EmulatorNamespace? Parse(string json) =>
        JsonSerializer.Deserialize<EmulatorConfigFile>(json, JsonOptions)?.UserConfig.Namespaces.FirstOrDefault();
}
