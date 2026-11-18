using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using TUnit.Core.Interfaces;

namespace ServiceBusConsole.IntegrationTests;

/// <summary>
/// The Service Bus emulator from the AppHost, once per test session, with this project's <c>emulator/Config.json</c>.
/// Runs only with <c>AZSB_INTEGRATION=1</c>, since it needs Docker and a minute to start.
/// </summary>
public sealed class Emulator : IAsyncInitializer, IAsyncDisposable
{
    public const string EnableVariable = "AZSB_INTEGRATION";

    private DistributedApplication? _app;
    private ServiceProvider? _services;

    public static bool Enabled => Environment.GetEnvironmentVariable(EnableVariable) == "1";

    public static string ConfigFile { get; } = Path.Combine(AppContext.BaseDirectory, "emulator", "Config.json");

    public IBrokerOpener Opener => _services?.GetRequiredService<IBrokerOpener>()
        ?? throw new InvalidOperationException($"The emulator runs only with {EnableVariable}=1.");

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
        _services = new ServiceCollection()
            .AddLogging()
            .AddAzureServiceBus(sb => sb.Emulator(connectionString, ConfigFile))
            .BuildServiceProvider();
    }

    public IBroker Open() => Opener.Open(BrokerInfo.Emulator);

    public async ValueTask DisposeAsync()
    {
        if (_services is not null) await _services.DisposeAsync();
        if (_app is not null) await _app.DisposeAsync();
    }
}
