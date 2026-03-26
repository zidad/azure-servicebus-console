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
    public bool IsEmulator => CurrentNamespace == "localhost";

    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    public void SetNamespace(string fullyQualifiedNamespace)
    {
        logger.LogInformation("Connecting to namespace {Namespace}", fullyQualifiedNamespace);
        CurrentNamespace = fullyQualifiedNamespace;
        if (fullyQualifiedNamespace == "localhost")
        {
            _adminClient = null; // management API not supported by emulator
            _busClient = new ServiceBusClient(EmulatorConnectionString);
        }
        else
        {
            _adminClient = new ServiceBusAdministrationClient($"https://{fullyQualifiedNamespace}", credential);
            _busClient = new ServiceBusClient($"sb://{fullyQualifiedNamespace}/", credential);
        }
    }

    public ServiceBusAdministrationClient AdminClient =>
        _adminClient ?? throw new InvalidOperationException(
            IsEmulator ? "Management API is not supported by the local emulator." : "No namespace selected.");

    public ServiceBusClient BusClient =>
        _busClient ?? throw new InvalidOperationException("No namespace selected");
}
