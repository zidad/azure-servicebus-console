using JasperFx.CommandLine;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

[Description("Open the terminal UI (the default command)")]
public sealed class RunCommand : JasperFxAsyncCommand<RunInput>
{
    public override async Task<bool> Execute(RunInput input)
    {
        if (input is { SubscriptionFlag: null, NamespaceFlag: not null })
        {
            AnsiConsole.MarkupLine("[red]Invalid usage:[/] --namespace needs --subscription. See [italic]azsb help run[/].");
            return false;
        }

        var host = input.BuildHost();
        host.Services.GetRequiredService<NavigationState>().Startup = input.StartupTarget;
        await host.Services.GetRequiredService<MetricsVisibility>().LoadAsync();
        await host.RunAsync();
        return true;
    }
}
