using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Azure Service Bus, signed in through the Azure CLI: the namespace listing and the <see cref="INamespaceOpener"/>.
    /// Registered <see cref="INamespaceDecorator"/>s wrap the services of each namespace it opens.
    /// </summary>
    public static IServiceCollection AddAzureServiceBus(this IServiceCollection services) => services
        .AddSingleton(new AzureCliCredential())
        .AddSingleton(sp => new ArmClient(sp.GetRequiredService<AzureCliCredential>()))
        .AddSingleton<INamespaceService, NamespaceService>()
        .AddSingleton<INamespaceOpener, NamespaceOpener>();
}
