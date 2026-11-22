using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RazorConsole.Core;
using ServiceBusConsole.Components;

namespace ServiceBusConsole;

/// <summary>The command line and terminal UI on one broker transport: an app's whole <c>Main</c>.</summary>
public static class TerminalApp
{
    public static Task<int> RunAsync(string[] args, BrokerTransport transport, IEnumerable<IPlugin> bundledPlugins)
    {
        var logFile = Path.Combine(
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs")).FullName,
            $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var plugins = new PluginCatalog(bundledPlugins);

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config => config.AddAzsbConfiguration(AppPaths.ConfigFile))
            .UseRazorConsole<App>()
            .ConfigureLogging(logging => logging
                .ClearProviders()
                .AddProvider(new FileLoggerProvider(logFile))
                .SetMinimumLevel(LogLevel.Debug)
                // Blazor logs every component render at Debug: hundreds of MB per session.
                .AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information))
            .ConfigureServices((context, services) => transport.AddServices(services, context.Configuration))
            .ConfigureServices(services => services
                .AddSingleton<IBrokerDecorator, CachingBrokerDecorator>()
                .AddSingleton<CurrentBroker>()
                .AddSingleton<FileCache>()
                .AddSingleton<NavigationState>()
                .AddSingleton<ConnectionList>()
                .AddSingleton<RecentBrokers>()
                .AddSingleton<BrokerSwitcher>()
                .AddSingleton<ExplorerTree>()
                .AddSingleton<MessageBrowserStore>()
                .AddSingleton<ActivityLog>()
                .AddSingleton<Clipboard>()
                .AddSingleton<SessionContext>()
                .AddSingleton<LiveCountsOptions>()
                .AddSingleton<MetricsOptions>()
                .AddSingleton<MetricsVisibility>()
                .AddSingleton<MetricsRange>()
                .AddSyntaxLanguages()
                .Decorate<IConnectionCatalog, CachingConnectionCatalog>()
                .Configure<ConsoleAppOptions>(options =>
                {
                    options.AutoClearConsole = true;
                    options.ConsoleLiveDisplayOptions.EnableMouseEvents = true;
                }))
            .ConfigureServices((context, services) => services.AddPlugins(plugins, context.Configuration));

        return AppCommands.ExecuteAsync(builder, plugins, transport, () => CommandServices.Create(transport, logging => logging
            .AddProvider(new FileLoggerProvider(logFile))
            .SetMinimumLevel(LogLevel.Information)), args);
    }
}
