namespace ServiceBusConsole.UI;

/// <summary>A namespace and the Azure subscription it is listed under.</summary>
public sealed record JumpNamespace(AzureSubscription Subscription, BrokerInfo Info)
{
    public bool Is(AzureSubscription? subscription, string? fullyQualifiedNamespace) =>
        Subscription == subscription && Info.FullyQualifiedNamespace == fullyQualifiedNamespace;
}
