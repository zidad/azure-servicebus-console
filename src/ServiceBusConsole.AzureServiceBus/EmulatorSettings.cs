namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Where the local emulator listens and which config file lists its entities: it has no management API.</summary>
public sealed record EmulatorSettings
{
    public const string LocalConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public string ConnectionString { get; init; } = LocalConnectionString;

    /// <summary>The emulator's <c>Config.json</c>; null: the nearest <c>emulator/Config.json</c> (see <c>EmulatorConfigReader.Find</c>).</summary>
    public string? ConfigFile { get; init; }
}
