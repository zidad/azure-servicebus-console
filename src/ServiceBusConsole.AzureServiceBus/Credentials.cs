using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>What <see cref="AzureOptions"/> picks: the credential and the client options.</summary>
internal static class Credentials
{
    public static TokenCredential For(AzureSignIn signIn) => signIn switch
    {
        AzureSignIn.Cli => new AzureCliCredential(),
        AzureSignIn.Browser => new InteractiveBrowserCredential(),
        AzureSignIn.Default => new DefaultAzureCredential(),
        _ => throw new ArgumentOutOfRangeException(nameof(signIn), signIn, null),
    };

    public static ServiceBusClientOptions ClientOptions(ServiceBusTransport transport) => new()
    {
        TransportType = transport switch
        {
            ServiceBusTransport.Tcp => ServiceBusTransportType.AmqpTcp,
            ServiceBusTransport.WebSockets => ServiceBusTransportType.AmqpWebSockets,
            _ => throw new ArgumentOutOfRangeException(nameof(transport), transport, null),
        },
    };
}
