namespace ServiceBusConsole.AzureServiceBus.Emulator;

internal sealed record EmulatorNamespaceDef
{
    public string Name { get; set; } = "";
    public IReadOnlyList<EmulatorQueueDef> Queues { get; set; } = [];
    public IReadOnlyList<EmulatorTopicDef> Topics { get; set; } = [];
}
