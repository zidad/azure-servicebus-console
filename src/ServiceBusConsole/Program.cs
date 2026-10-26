using Azure.Identity;
using Azure.ResourceManager;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RazorConsole.Core;
using ServiceBusConsole.Components;

var logFile = Path.Combine(
    Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, "logs")).FullName,
    $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");

await Host.CreateDefaultBuilder(args)
    .UseRazorConsole<App>()
    .ConfigureLogging(logging => logging
        .ClearProviders()
        .AddProvider(new FileLoggerProvider(logFile))
        .SetMinimumLevel(LogLevel.Debug)
        // Blazor logs every component render at Debug: hundreds of MB per session.
        .AddFilter("Microsoft.AspNetCore.Components", LogLevel.Information))
    .ConfigureServices(services => services
        .AddSingleton(new AzureCliCredential())
        .AddSingleton(sp => new ArmClient(sp.GetRequiredService<AzureCliCredential>()))
        .AddSingleton<ServiceBusConnection>()
        .AddSingleton<FileCache>()
        .AddSingleton<INamespaceService, NamespaceService>()
        .AddSingleton<IQueueService, QueueService>()
        .AddSingleton<ITopicService, TopicService>()
        .AddSingleton<IMessageService, MessageService>()
        .AddSingleton<NavigationState>()
        .AddSingleton<SubscriptionCatalog>()
        .AddSingleton<MessageBrowserStore>()
        .AddSingleton<LiveCountsOptions>()
        .AddSyntaxLanguages()
        .Decorate<INamespaceService, CachingNamespaceService>()
        .Decorate<IQueueService, CachingQueueService>()
        .Decorate<ITopicService, CachingTopicService>()
        .Configure<ConsoleAppOptions>(options =>
        {
            options.AutoClearConsole = true;
            options.ConsoleLiveDisplayOptions.EnableMouseEvents = true;
        }))
    .Build()
    .RunAsync();
