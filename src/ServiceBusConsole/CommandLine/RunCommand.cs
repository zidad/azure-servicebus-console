using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.CommandLine;

/// <summary>Opens the terminal UI, the default command.</summary>
public static class RunCommand
{
    public static async Task<bool> ExecuteAsync(RunInput input)
    {
        if (input is { Subscription: null, Namespace: not null })
        {
            AnsiConsole.MarkupLine("[red]Invalid usage:[/] --namespace needs --subscription. See [italic]azsb help run[/].");
            return false;
        }

        var host = input.HostBuilder.Build();
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
