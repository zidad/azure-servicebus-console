using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorUserConfig
{
    [JsonPropertyName("Namespaces")] public List<EmulatorNamespace> Namespaces { get; set; } = [];
}
