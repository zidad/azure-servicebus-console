namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal sealed record EmulatorUserConfig
{
    public IReadOnlyList<EmulatorNamespaceDef> Namespaces { get; set; } = [];
}
