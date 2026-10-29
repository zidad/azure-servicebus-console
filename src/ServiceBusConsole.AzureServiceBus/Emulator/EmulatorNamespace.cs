namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal sealed record EmulatorNamespace
{
    public string Name { get; init; } = "";
    public IReadOnlyList<EmulatorQueueDef> Queues { get; init; } = [];
    public IReadOnlyList<EmulatorTopicDef> Topics { get; init; } = [];
}
