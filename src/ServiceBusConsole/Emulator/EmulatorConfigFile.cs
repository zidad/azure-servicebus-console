using System.Text.Json.Serialization;

namespace ServiceBusConsole.Emulator;

public class EmulatorConfigFile
{
    [JsonPropertyName("UserConfig")] public EmulatorUserConfig UserConfig { get; set; } = new();
}
