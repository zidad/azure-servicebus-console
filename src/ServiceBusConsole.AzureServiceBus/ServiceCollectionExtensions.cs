using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceBusConsole.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Azure Service Bus, signed in as the registered <see cref="AzureOptions"/> say (the Azure CLI over TCP without one):
    /// the namespace listing and the <see cref="INamespaceOpener"/>. Registered <see cref="INamespaceDecorator"/>s wrap the
    /// services of each namespace it opens.
    /// </summary>
    public static IServiceCollection AddAzureServiceBus(this IServiceCollection services)
    {
        services.TryAddSingleton(new AzureOptions());
        return services
            .AddSingleton(sp => Credentials.For(sp.GetRequiredService<AzureOptions>().SignIn))
            .AddSingleton(sp => new ArmClient(sp.GetRequiredService<TokenCredential>()))
            .AddSingleton<INamespaceService, NamespaceService>()
            .AddSingleton<INamespaceOpener, NamespaceOpener>();
    }
}
