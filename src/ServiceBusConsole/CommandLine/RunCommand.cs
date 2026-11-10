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
            InvalidUsage(CommandLineStrings.FormatRunCommand_Needs("--namespace", "--subscription", HelpRun));
            return false;
        }
        if (input is { Namespace: null, Open: not null })
        {
            InvalidUsage(CommandLineStrings.FormatRunCommand_Needs("--open", "--namespace", HelpRun));
            return false;
        }

        var host = input.HostBuilder.Build();
        var theme = input.ThemeSetting(host.Services.GetRequiredService<IConfiguration>());
        if (Palette.For(theme, Environment.GetEnvironmentVariable("COLORFGBG")) is not { } palette)
        {
            InvalidUsage(CommandLineStrings.FormatRunCommand_InvalidTheme(Markup.Escape(theme ?? ""), ConfigKeys.Theme.Key));
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

    private const string HelpRun = "[italic]azsb help run[/]";

    private static void InvalidUsage(string markup) =>
        AnsiConsole.MarkupLine($"[red]{Markup.Escape(CommandLineStrings.RunCommand_InvalidUsage)}[/] {markup}");
}
