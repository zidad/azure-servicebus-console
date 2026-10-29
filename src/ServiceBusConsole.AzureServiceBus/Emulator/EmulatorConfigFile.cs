namespace ServiceBusConsole.AzureServiceBus.Emulator;

/// <summary>The emulator's <c>Config.json</c>.</summary>
internal sealed record EmulatorConfigFile
{
    public EmulatorUserConfig UserConfig { get; init; } = new();
}
