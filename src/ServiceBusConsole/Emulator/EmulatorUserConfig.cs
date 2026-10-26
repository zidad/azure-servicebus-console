namespace ServiceBusConsole.Emulator;

public sealed record EmulatorUserConfig
{
    public IReadOnlyList<EmulatorNamespace> Namespaces { get; init; } = [];
}
