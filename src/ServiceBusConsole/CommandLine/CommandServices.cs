using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceBusConsole.AzureServiceBus;

namespace ServiceBusConsole.CommandLine;

/// <summary>What the namespace commands use: live Azure (no cache, so output is current), the settings and the output streams.</summary>
public sealed record CommandServices(INamespaceService Namespaces, INamespaceOpener Opener, IConfiguration Configuration)
{
    public TextWriter Out { get; init; } = Console.Out;

    public TextWriter Error { get; init; } = Console.Error;

    /// <summary>Standard input when something is piped in, for <c>send</c>; null at a terminal.</summary>
    public Func<Stream?> Input { get; init; } = () => Console.IsInputRedirected ? Console.OpenStandardInput() : null;

    public static CommandServices Create(Action<ILoggingBuilder> logging)
    {
        var configuration = new ConfigurationBuilder().AddAzsbConfiguration(AppPaths.ConfigFile).Build();
        var services = new ServiceCollection()
            .AddLogging(logging)
            .AddSingleton(AzureSettings.Read(configuration))
            .AddAzureServiceBus()
            .BuildServiceProvider();
        return new(services.GetRequiredService<INamespaceService>(), services.GetRequiredService<INamespaceOpener>(), configuration);
    }
}
