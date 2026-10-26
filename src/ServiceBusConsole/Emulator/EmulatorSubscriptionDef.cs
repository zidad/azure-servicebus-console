using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorSubscriptionDef
{
    [JsonPropertyName("Name")] public string Name { get; set; } = "";
}
