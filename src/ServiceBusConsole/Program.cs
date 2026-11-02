using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RazorConsole.Core;
using ServiceBusConsole.AzureServiceBus;
using ServiceBusConsole.Components;

var logFile = Path.Combine(
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs")).FullName,
    $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureAppConfiguration(config => config.AddAzsbConfiguration(AppPaths.ConfigFile))
    .UseRazorConsole<App>()
    .ConfigureLogging(logging => logging
        .ClearProviders()
        .AddProvider(new FileLoggerProvider(logFile))
        .SetMinimumLevel(LogLevel.Debug)
        // Blazor logs every component render at Debug: hundreds of MB per session.
        .AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information))
    .ConfigureServices(services => services
        .AddAzureServiceBus()
        .AddSingleton<INamespaceDecorator, CachingNamespaceDecorator>()
        .AddSingleton<ServiceBusConnection>()
        .AddSingleton<FileCache>()
        .AddSingleton<NavigationState>()
        .AddSingleton<SubscriptionCatalog>()
        .AddSingleton<ExplorerTree>()
        .AddSingleton<MessageBrowserStore>()
        .AddSingleton<LiveCountsOptions>()
        .AddSingleton<MetricsOptions>()
        .AddSingleton<MetricsVisibility>()
        .AddSingleton<MetricsRange>()
        .AddSyntaxLanguages()
        .Decorate<INamespaceService, CachingNamespaceService>()
        .Configure<ConsoleAppOptions>(options =>
        {
            options.AutoClearConsole = true;
            options.ConsoleLiveDisplayOptions.EnableMouseEvents = true;
        }));

return await AppCommands.ExecuteAsync(builder, args);
