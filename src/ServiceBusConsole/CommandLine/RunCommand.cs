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

        if (Palette.For(input.ThemeFlag, Environment.GetEnvironmentVariable("COLORFGBG")) is not { } palette)
        {
            AnsiConsole.MarkupLine("[red]Invalid usage:[/] --theme is auto, dark or light.");
            return false;
        }
        Theme.Use(palette);

        var host = input.BuildHost();
        host.Services.GetRequiredService<NavigationState>().Startup = input.StartupTarget;
        await host.Services.GetRequiredService<MetricsVisibility>().LoadAsync();
        await host.Services.GetRequiredService<MetricsRange>().LoadAsync();
        await host.RunAsync();
        return true;
    }
}
