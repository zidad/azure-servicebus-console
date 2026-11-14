using Azure.Core;
using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class AzureOptionsTests
{
    private static IServiceCollection Services() => new ServiceCollection()
        .AddSingleton<ILoggerFactory>(NullLoggerFactory.Instance)
        .AddSingleton(typeof(ILogger<>), typeof(NullLogger<>));

    private static TokenCredential Registered(params AzureOptions[] options)
    {
        using var provider = options.Aggregate(Services(), (s, o) => s.AddSingleton(o)).AddAzureServiceBus().BuildServiceProvider();
        return provider.GetRequiredService<TokenCredential>();
    }

    [Test]
    public void WithoutOptions_SignsInWithTheAzureCli() =>
        Registered().ShouldBeOfType<AzureCliCredential>();

    [Test]
    public void RegisteredOptions_PickTheCredential()
    {
        Registered(new AzureOptions { SignIn = AzureSignIn.Browser }).ShouldBeOfType<InteractiveBrowserCredential>();
        Registered(new AzureOptions { SignIn = AzureSignIn.Default }).ShouldBeOfType<DefaultAzureCredential>();
    }

    [Test]
    public void OpenerIsResolvedWithTheOptions()
    {
        using var provider = Services()
            .AddSingleton(new AzureOptions { Transport = ServiceBusTransport.WebSockets })
            .AddAzureServiceBus()
            .BuildServiceProvider();

        provider.GetRequiredService<INamespaceOpener>().ShouldBeOfType<NamespaceOpener>();
    }

    [Test]
    [Arguments(ServiceBusTransport.Tcp, ServiceBusTransportType.AmqpTcp)]
    [Arguments(ServiceBusTransport.WebSockets, ServiceBusTransportType.AmqpWebSockets)]
    public void Transport_SetsTheClientsTransportType(ServiceBusTransport transport, ServiceBusTransportType expected) =>
        Credentials.ClientOptions(transport).TransportType.ShouldBe(expected);
}
