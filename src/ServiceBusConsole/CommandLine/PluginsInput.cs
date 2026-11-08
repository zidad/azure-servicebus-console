using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.CommandLine;

public sealed record PluginsInput : CommandInput
{
    public PluginCatalog Catalog { get; init; } = PluginCatalog.Empty;

    /// <summary>The settings as the app reads them: config file and environment variables.</summary>
    public IConfiguration Configuration { get; init; } = new ConfigurationBuilder().Build();

    public TextWriter Out { get; init; } = Console.Out;
}
