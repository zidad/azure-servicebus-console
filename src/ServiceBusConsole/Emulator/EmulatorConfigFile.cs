namespace ServiceBusConsole.Emulator;

/// <summary>The emulator's <c>Config.json</c>.</summary>
public sealed record EmulatorConfigFile
{
    public EmulatorUserConfig UserConfig { get; init; } = new();
}
