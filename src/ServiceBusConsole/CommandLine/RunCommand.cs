using JasperFx.CommandLine;
using Microsoft.Extensions.Configuration;
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
        var theme = input.ThemeSetting(host.Services.GetRequiredService<IConfiguration>());
        if (Palette.For(theme, Environment.GetEnvironmentVariable("COLORFGBG")) is not { } palette)
        {
            AnsiConsole.MarkupLine($"[red]Invalid usage:[/] theme '{Markup.Escape(theme ?? "")}' is not auto, dark or light (--theme, else {ConfigKeys.Theme.Key}).");
            host.Dispose();
            return false;
        }
        Theme.Use(palette);

        host.Services.GetRequiredService<NavigationState>().Startup = input.StartupTarget;
        await host.Services.GetRequiredService<MetricsVisibility>().LoadAsync();
        await host.Services.GetRequiredService<MetricsRange>().LoadAsync();
        await host.RunAsync();
        return true;
    }
}
