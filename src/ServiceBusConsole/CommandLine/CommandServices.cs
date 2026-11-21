using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.CommandLine;

/// <summary>What the namespace commands use: the live broker (no cache, so output is current), the settings and the output streams.</summary>
public sealed record CommandServices(IConnectionCatalog Namespaces, IBrokerOpener Opener, IConfiguration Configuration)
{
    public TextWriter Out { get; init; } = Console.Out;

    public TextWriter Error { get; init; } = Console.Error;

    /// <summary>Standard input when something is piped in, for <c>send</c>; null at a terminal.</summary>
    public Func<Stream?> Input { get; init; } = () => Console.IsInputRedirected ? Console.OpenStandardInput() : null;

    public static CommandServices Create(AppIdentity app, BrokerTransport transport, Action<ILoggingBuilder> logging)
    {
        var configuration = new ConfigurationBuilder().AddAppConfiguration(app).Build();
        var services = new ServiceCollection().AddLogging(logging);
        transport.AddServices(services, configuration);
        var provider = services.BuildServiceProvider();
        return new(provider.GetRequiredService<IConnectionCatalog>(), provider.GetRequiredService<IBrokerOpener>(), configuration);
    }
}
