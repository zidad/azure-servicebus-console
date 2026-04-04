using System.Text.Json;
using System.Text.Json.Serialization;

namespace ServiceBusConsole;

public class EmulatorConfigFile
{
    [JsonPropertyName("UserConfig")] public EmulatorUserConfig UserConfig { get; set; } = new();
}

public class EmulatorUserConfig
{
    [JsonPropertyName("Namespaces")] public List<EmulatorNamespace> Namespaces { get; set; } = [];
}

public class EmulatorNamespace
{
    [JsonPropertyName("Name")]   public string Name { get; set; } = "";
    [JsonPropertyName("Queues")] public List<EmulatorQueueDef> Queues { get; set; } = [];
    [JsonPropertyName("Topics")] public List<EmulatorTopicDef> Topics { get; set; } = [];
}

public class EmulatorQueueDef
{
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
}

public class EmulatorTopicDef
{
    [JsonPropertyName("Name")]          public string Name { get; set; } = "";
    [JsonPropertyName("Subscriptions")] public List<EmulatorSubscriptionDef> Subscriptions { get; set; } = [];
}

public class EmulatorSubscriptionDef
{
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
}

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
