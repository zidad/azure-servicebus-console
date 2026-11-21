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
    public static Task<int> RunAsync(string[] args, AppIdentity app, BrokerTransport transport, IEnumerable<IPlugin> bundledPlugins)
    {
        var logFile = Path.Combine(
            Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs")).FullName,
            $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");

        var plugins = new PluginCatalog(bundledPlugins);

        var builder = Host.CreateDefaultBuilder(args)
            .ConfigureAppConfiguration(config => config.AddAppConfiguration(app))
            .UseRazorConsole<App>()
            .ConfigureLogging(logging => logging
                .ClearProviders()
                .AddProvider(new FileLoggerProvider(logFile))
                .SetMinimumLevel(LogLevel.Debug)
                // Blazor logs every component render at Debug: hundreds of MB per session.
                .AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information))
            .ConfigureServices((context, services) => transport.AddServices(services, context.Configuration))
            .ConfigureServices(services => services
                .AddSingleton(app)
                .AddSingleton<IBrokerDecorator, CachingNamespaceDecorator>()
                .AddSingleton<ServiceBusConnection>()
                .AddSingleton<FileCache>()
                .AddSingleton<NavigationState>()
                .AddSingleton<SubscriptionCatalog>()
                .AddSingleton<RecentNamespaces>()
                .AddSingleton<NamespaceSwitcher>()
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
                .Decorate<IConnectionCatalog, CachingNamespaceService>()
                .Configure<ConsoleAppOptions>(options =>
                {
                    options.AutoClearConsole = true;
                    options.ConsoleLiveDisplayOptions.EnableMouseEvents = true;
                }))
            .ConfigureServices((context, services) => services.AddPlugins(plugins, context.Configuration, app.StateDirectory));

        return AppCommands.ExecuteAsync(builder, app, plugins, transport, () => CommandServices.Create(app, transport, logging => logging
            .AddProvider(new FileLoggerProvider(logFile))
            .SetMinimumLevel(LogLevel.Information)), args);
    }
}
