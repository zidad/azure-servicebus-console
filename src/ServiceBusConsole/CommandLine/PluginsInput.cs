using JasperFx.CommandLine;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.CommandLine;

public sealed class PluginsInput
{
    [IgnoreOnCommandLine]
    public PluginCatalog Catalog { get; set; } = PluginCatalog.Empty;

    /// <summary>The settings as the app reads them: config file and environment variables.</summary>
    [IgnoreOnCommandLine]
    public IConfiguration Configuration { get; set; } = new ConfigurationBuilder().Build();

    [IgnoreOnCommandLine]
    public TextWriter Out { get; set; } = Console.Out;
}
