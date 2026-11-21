using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using ServiceBusConsole.RabbitMQ;
using TUnit.Core.Interfaces;

namespace ServiceBusConsole.IntegrationTests;

/// <summary>
/// The AppHost's brokers, once per test session: the Service Bus emulator with this project's <c>emulator/Config.json</c>,
/// and RabbitMQ with its management plugin. Runs only with <c>AZSB_INTEGRATION=1</c>, since it needs Docker and a minute to start.
/// </summary>
public sealed class Brokers : IAsyncInitializer, IAsyncDisposable
{
    public const string EnableVariable = "AZSB_INTEGRATION";

    private DistributedApplication? _app;
    private ServiceProvider? _emulator;
    private ServiceProvider? _rabbitMq;

    public static bool Enabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";

    public static string ConfigFile { get; } = Path.Combine(AppContext.BaseDirectory, "emulator", "Config.json");

    /// <summary>Where RabbitMQ listens, with its generated password.</summary>
    public RabbitMqOptions RabbitMq => _rabbitMq?.GetRequiredService<RabbitMqOptions>() ?? throw NotStarted();

    public async Task InitializeAsync()
    {
        if (!Enabled) return;
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(5));
        var builder = await DistributedApplicationTestingBuilder.CreateAsync<Projects.ServiceBusConsole_AppHost>(
            [
                $"--EmulatorConfig={ConfigFile}",
                // DCP's TLS reads the developer certificate's private key: on macOS a keychain prompt nobody answers.
                "--ASPIRE_DCP_USE_DEVELOPER_CERTIFICATE=false",
            ],
            (options, _) => options.DisableDashboard = true,
            timeout.Token);
        builder.Services.AddLogging(logging => logging
            .AddSimpleConsole(console => console.SingleLine = true)
            .AddFilter("Aspire", LogLevel.Information)
            .AddFilter("Microsoft", LogLevel.Warning));
        _app = await builder.BuildAsync(timeout.Token);
        await _app.StartAsync(timeout.Token);

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("servicebus", timeout.Token);
        var connectionString = await _app.GetConnectionStringAsync("servicebus", timeout.Token)
            ?? throw new InvalidOperationException("The AppHost gave the emulator no connection string.");
        _emulator = new ServiceCollection()
            .AddLogging()
            .AddAzureServiceBus(sb => sb.Emulator(connectionString, ConfigFile))
            .BuildServiceProvider();

        await _app.ResourceNotifications.WaitForResourceHealthyAsync("rabbitmq", timeout.Token);
        var amqp = new Uri(await _app.GetConnectionStringAsync("rabbitmq", timeout.Token)
            ?? throw new InvalidOperationException("The AppHost gave RabbitMQ no connection string."));
        var credentials = Uri.UnescapeDataString(amqp.UserInfo).Split(':', 2);
        _rabbitMq = new ServiceCollection()
            .AddLogging()
            .AddSingleton(new RabbitMqOptions
            {
                Amqp = new UriBuilder(amqp) { UserName = "", Password = "" }.Uri,
                Management = _app.GetEndpoint("rabbitmq", "management"),
                User = credentials[0],
                Password = credentials[1],
            })
            .AddRabbitMq()
            .BuildServiceProvider();
    }

    public IBroker OpenEmulator() => Opener(_emulator).Open(BrokerInfo.Emulator);

    public IConnectionCatalog RabbitMqServer => _rabbitMq?.GetRequiredService<IConnectionCatalog>() ?? throw NotStarted();

    public IBroker OpenRabbitMq(string vhost = "/") =>
        Opener(_rabbitMq).Open(new BrokerInfo { Name = vhost, FullyQualifiedNamespace = $"{RabbitMq.Management.Authority}/{Uri.EscapeDataString(vhost)}" });

    private static IBrokerOpener Opener(ServiceProvider? services) => services?.GetRequiredService<IBrokerOpener>() ?? throw NotStarted();

    private static InvalidOperationException NotStarted() => new($"The brokers run only with {EnableVariable}=1.");

    public async ValueTask DisposeAsync()
    {
        if (_emulator is not null) await _emulator.DisposeAsync();
        if (_rabbitMq is not null) await _rabbitMq.DisposeAsync();
        if (_app is not null) await _app.DisposeAsync();
    }
}
