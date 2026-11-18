using Azure.Core;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceBusConsole.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Azure Service Bus, signed in as the registered <see cref="AzureOptions"/> say (the Azure CLI over TCP without one):
    /// the namespace listing and the <see cref="IBrokerOpener"/>. Registered <see cref="IBrokerDecorator"/>s wrap the
    /// services of each namespace it opens.
    /// </summary>
    public static IServiceCollection AddAzureServiceBus(this IServiceCollection services, Action<AzureServiceBusBuilder>? configure = null)
    {
        var builder = new AzureServiceBusBuilder();
        configure?.Invoke(builder);
        services.TryAddSingleton(new AzureOptions());
        return services
            .AddSingleton(builder.EmulatorSettings)
            .AddSingleton(sp => Credentials.For(sp.GetRequiredService<AzureOptions>().SignIn))
            .AddSingleton(sp => new ArmClient(sp.GetRequiredService<TokenCredential>()))
            .AddSingleton<IConnectionCatalog, NamespaceService>()
            .AddSingleton<IBrokerOpener, NamespaceOpener>();
    }
}
