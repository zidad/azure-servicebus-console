using Azure.Identity;
using Azure.ResourceManager;
using LLMAgentTUI.Components;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RazorConsole.Core;
using ServiceBusConsole;

var logDir = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDir);
var logFile = Path.Combine(logDir, $"session-{DateTime.Now:yyyyMMdd-HHmmss}.log");

IHostBuilder hostBuilder = Host.CreateDefaultBuilder(args)
    .UseRazorConsole<App>()
    .ConfigureLogging(logging =>
    {
        logging.ClearProviders();
        logging.AddProvider(new FileLoggerProvider(logFile));
        logging.SetMinimumLevel(LogLevel.Debug);
    });

hostBuilder.ConfigureServices(services =>
{
    var credential = new AzureCliCredential();
    services.AddSingleton(credential);
    services.AddSingleton(new ArmClient(credential));
    services.AddSingleton<ServiceBusConnection>();
    services.AddSingleton<FileCache>();

    services.AddSingleton<INamespaceService, NamespaceService>();
    services.AddSingleton<IQueueService, QueueService>();
    services.AddSingleton<ITopicService, TopicService>();
    services.AddSingleton<IMessageService, MessageService>();

    services.Decorate<INamespaceService, CachingNamespaceService>();
    services.Decorate<IQueueService, CachingQueueService>();
    services.Decorate<ITopicService, CachingTopicService>();

    services.Configure<ConsoleAppOptions>(options =>
    {
        options.AutoClearConsole = true;
    });
});

var host = hostBuilder.Build();
await host.RunAsync();
