using System.Text.Json.Serialization;

namespace ServiceBusConsole.AzureServiceBus.Emulator;

// The config records default with set, not init: generated deserialization resets a missing init property to null.
[JsonSourceGenerationOptions(PropertyNameCaseInsensitive = true)]
[JsonSerializable(typeof(EmulatorConfigFile))]
internal sealed partial class EmulatorJsonContext : JsonSerializerContext;
