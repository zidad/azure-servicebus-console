using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole;

public class ServiceBusConnection(AzureCliCredential credential, ILogger<ServiceBusConnection> logger)
{
    private ServiceBusAdministrationClient? _adminClient;
    private ServiceBusClient? _busClient;

    public string? CurrentNamespace { get; private set; }

    public void SetNamespace(string fullyQualifiedNamespace)
    {
        logger.LogInformation("Connecting to namespace {Namespace}", fullyQualifiedNamespace);
        CurrentNamespace = fullyQualifiedNamespace;
        _adminClient = new ServiceBusAdministrationClient($"https://{fullyQualifiedNamespace}", credential);
        _busClient = new ServiceBusClient($"sb://{fullyQualifiedNamespace}/", credential);
    }

    public ServiceBusAdministrationClient AdminClient =>
        _adminClient ?? throw new InvalidOperationException("No namespace selected");

    public ServiceBusClient BusClient =>
        _busClient ?? throw new InvalidOperationException("No namespace selected");
}
