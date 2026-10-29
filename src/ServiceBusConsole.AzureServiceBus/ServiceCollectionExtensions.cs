using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.AzureServiceBus;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// The services on Azure Service Bus, signed in through the Azure CLI. They act on the namespace of the
    /// registered <see cref="ServiceBusConnection"/>.
    /// </summary>
    public static IServiceCollection AddAzureServiceBus(this IServiceCollection services) => services
        .AddSingleton(new AzureCliCredential())
        .AddSingleton(sp => new ArmClient(sp.GetRequiredService<AzureCliCredential>()))
        .AddSingleton<ServiceBusClients>()
        .AddSingleton<INamespaceService, NamespaceService>()
        .AddSingleton<IQueueService, QueueService>()
        .AddSingleton<ITopicService, TopicService>()
        .AddSingleton<IMessageService, MessageService>();
}
