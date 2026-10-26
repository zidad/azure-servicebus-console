using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorTopicDef
{
    [JsonPropertyName("Name")]          public string Name { get; set; } = "";
    [JsonPropertyName("Subscriptions")] public List<EmulatorSubscriptionDef> Subscriptions { get; set; } = [];
}
