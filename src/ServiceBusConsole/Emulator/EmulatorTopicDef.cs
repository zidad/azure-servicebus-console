namespace ServiceBusConsole.Emulator;

public sealed record EmulatorTopicDef
{
    public required string Name { get; init; }
    public IReadOnlyList<EmulatorSubscriptionDef> Subscriptions { get; init; } = [];
}
