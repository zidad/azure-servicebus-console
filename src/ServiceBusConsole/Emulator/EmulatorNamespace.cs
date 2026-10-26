using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorNamespace
{
    [JsonPropertyName("Name")]   public string Name { get; set; } = "";
    [JsonPropertyName("Queues")] public List<EmulatorQueueDef> Queues { get; set; } = [];
    [JsonPropertyName("Topics")] public List<EmulatorTopicDef> Topics { get; set; } = [];
}
