using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ServiceBusConsole.PluginHost;

public static class PluginServiceCollectionExtensions
{
    /// <summary>
    /// The enabled plugins' services, <see cref="PluginActions"/>, and an <see cref="IPluginHost"/> running real processes
    /// in <paramref name="stateDirectory"/> unless one is registered already. Disabled plugins register nothing.
    /// </summary>
    public static IServiceCollection AddPlugins(this IServiceCollection services, PluginCatalog catalog, IConfiguration configuration, string stateDirectory)
    {
        foreach (var plugin in catalog.Enabled(configuration)) plugin.ConfigureServices(services);
        services.TryAddSingleton<IPluginHost>(new ProcessPluginHost(stateDirectory));
        return services
            .AddSingleton(catalog)
            .AddSingleton(sp => new PluginActions(sp.GetServices<IMessageAction>(), catalog, configuration, sp.GetRequiredService<IPluginHost>()));
    }
}
