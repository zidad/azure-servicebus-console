using System.Text.Json;

namespace ServiceBusConsole.Emulator;

public static class EmulatorConfigReader
{
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public static EmulatorNamespace? TryLoad()
    {
        var path = Path.Combine(Directory.GetCurrentDirectory(), "emulator", "Config.json");
        if (!File.Exists(path)) return null;
        var config = JsonSerializer.Deserialize<EmulatorConfigFile>(File.ReadAllText(path), JsonOpts);
        return config?.UserConfig.Namespaces.FirstOrDefault();
    }
}
