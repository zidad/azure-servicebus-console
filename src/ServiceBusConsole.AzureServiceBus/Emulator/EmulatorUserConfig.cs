namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal sealed record EmulatorUserConfig
{
    public IReadOnlyList<EmulatorNamespace> Namespaces { get; init; } = [];
}
