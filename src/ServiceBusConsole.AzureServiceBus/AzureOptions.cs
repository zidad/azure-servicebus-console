namespace ServiceBusConsole.AzureServiceBus;

/// <summary>How <see cref="ServiceCollectionExtensions.AddAzureServiceBus"/> connects; register one to change the defaults.</summary>
public sealed record AzureOptions
{
    public AzureSignIn SignIn { get; init; } = AzureSignIn.Cli;

    public ServiceBusTransport Transport { get; init; } = ServiceBusTransport.Tcp;
}
