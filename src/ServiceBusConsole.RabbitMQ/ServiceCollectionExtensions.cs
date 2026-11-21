using EasyNetQ.Management.Client;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceBusConsole.RabbitMQ;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// RabbitMQ, as the registered <see cref="RabbitMqOptions"/> say (guest on localhost without one): its virtual hosts
    /// as the namespaces of one server, and the <see cref="IBrokerOpener"/>. Registered <see cref="IBrokerDecorator"/>s
    /// wrap the services of each virtual host it opens.
    /// </summary>
    public static IServiceCollection AddRabbitMq(this IServiceCollection services)
    {
        services.TryAddSingleton(new RabbitMqOptions());
        return services
            .AddSingleton<IManagementClient>(sp => Management(sp.GetRequiredService<RabbitMqOptions>()))
            .AddSingleton<IConnectionCatalog, ServerCatalog>()
            .AddSingleton<IBrokerOpener, VhostOpener>();
    }

    private static ManagementClient Management(RabbitMqOptions options) => new(options.Management, options.User, options.Password);
}
