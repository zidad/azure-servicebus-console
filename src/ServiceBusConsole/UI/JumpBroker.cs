namespace ServiceBusConsole.UI;

/// <summary>A namespace and the Azure subscription it is listed under.</summary>
public sealed record JumpBroker(ConnectionInfo Subscription, BrokerInfo Info)
{
    public bool Is(ConnectionInfo? subscription, string? fullyQualifiedNamespace) =>
        Subscription == subscription && Info.Address == fullyQualifiedNamespace;
}
