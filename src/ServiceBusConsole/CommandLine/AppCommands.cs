using JasperFx.CommandLine;
using JasperFx.CommandLine.Help;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The azsb commands: <see cref="RunCommand"/>, the default, <see cref="ConfigCommand"/>, <see cref="PluginsCommand"/> and help. Not JasperFx's <c>RunJasperFxCommands</c>: that scans
/// the app folder for command assemblies and registers JasperFx's own <c>run</c> last, over this one.
/// </summary>
public sealed class AppCommands : CommandFactory
{
    private const string AppName = "azsb";

    public AppCommands(IHostBuilder host) : this(host, PluginCatalog.Empty) { }

    public AppCommands(IHostBuilder host, PluginCatalog plugins)
    {
        DefaultCommand = typeof(RunCommand);
        RegisterCommand<ConfigCommand>();
        RegisterCommand<PluginsCommand>();
        ConfigureRun = run =>
        {
            switch (run.Input)
            {
                case IHostBuilderInput input:
                    input.HostBuilder = host;
                    break;
                case ConfigInput config:
                    config.Keys = ConfigKeys.With(plugins.ConfigKeys);
                    break;
                case PluginsInput list:
                    list.Catalog = plugins;
                    list.Configuration = new ConfigurationBuilder().AddAzsbConfiguration(AppPaths.ConfigFile).Build();
                    break;
            }
        };
    }

    public static Task<int> ExecuteAsync(IHostBuilder host, PluginCatalog plugins, string[] args) =>
        new CommandExecutor(new AppCommands(host, plugins)).ExecuteAsync(args);

    public override CommandRun HelpRun(Queue<string> queue)
    {
        var run = base.HelpRun(queue);
        if (run.Input is HelpInput help) help.AppName = AppName;
        return run;
    }
}
