using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.AzureServiceBus;

namespace ServiceBusConsole.Azsb;

/// <summary>Azure Service Bus as azsb's broker, with its <c>azure.*</c> settings.</summary>
public static class AzureTransport
{
    public static ConfigKey Auth { get; } = new("azure.auth", Strings.AzureTransport_Auth, "cli");

    public static ConfigKey Transport { get; } = new("azure.transport", Strings.AzureTransport_Transport, "tcp");

    public static BrokerTransport Broker { get; } = new(ServiceBusTerms.Terms, [Auth, Transport], (services, configuration) => services
        .AddSingleton(_ => AzureSettings.Read(configuration))
        .AddAzureServiceBus());
}
