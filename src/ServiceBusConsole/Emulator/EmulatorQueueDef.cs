using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorQueueDef
{
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
}
