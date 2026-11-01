using JasperFx.CommandLine;
using JasperFx.CommandLine.Help;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

/// <summary>
/// The azsb commands: <see cref="RunCommand"/>, the default, and help. Not JasperFx's <c>RunJasperFxCommands</c>: that scans
/// the app folder for command assemblies and registers JasperFx's own <c>run</c> last, over this one.
/// </summary>
public sealed class AppCommands : CommandFactory
{
    private const string AppName = "azsb";

    public AppCommands(IHostBuilder host)
    {
        DefaultCommand = typeof(RunCommand);
        ConfigureRun = run =>
        {
            if (run.Input is IHostBuilderInput input) input.HostBuilder = host;
        };
    }

    public static Task<int> ExecuteAsync(IHostBuilder host, string[] args) => new CommandExecutor(new AppCommands(host)).ExecuteAsync(args);

    public override CommandRun HelpRun(Queue<string> queue)
    {
        var run = base.HelpRun(queue);
        if (run.Input is HelpInput help) help.AppName = AppName;
        return run;
    }
}
