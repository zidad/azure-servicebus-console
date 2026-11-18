namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Configures <see cref="ServiceCollectionExtensions.AddAzureServiceBus"/>.</summary>
public sealed class AzureServiceBusBuilder
{
    internal EmulatorSettings EmulatorSettings { get; private set; } = new();

    /// <summary>A local emulator other than the one on <c>localhost:5672</c>, such as one started by Aspire.</summary>
    public AzureServiceBusBuilder Emulator(string connectionString, string? configFile = null)
    {
        EmulatorSettings = new EmulatorSettings { ConnectionString = connectionString, ConfigFile = configFile };
        return this;
    }
}
