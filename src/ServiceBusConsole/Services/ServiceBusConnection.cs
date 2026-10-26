using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.Services;

public class ServiceBusConnection(AzureCliCredential credential, ILogger<ServiceBusConnection> logger)
{
    private ServiceBusAdministrationClient? _adminClient;
    private ServiceBusClient? _busClient;

    public NamespaceInfo? Namespace { get; private set; }

    /// <summary>Changes on <see cref="Switch"/>; the app keys the current page on it so the page reloads.</summary>
    public int Version { get; private set; }

    public event Action? Switched;
    public string? CurrentNamespace => Namespace?.FullyQualifiedNamespace;
    public bool IsEmulator => CurrentNamespace == "localhost";

    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    /// <summary>Sets the namespace and reloads the current page (see <see cref="Version"/>).</summary>
    public void Switch(NamespaceInfo ns)
    {
        SetNamespace(ns);
        Version++;
        Switched?.Invoke();
    }

    public void SetNamespace(NamespaceInfo ns)
    {
        var fullyQualifiedNamespace = ns.FullyQualifiedNamespace;
        var sameEndpoint = fullyQualifiedNamespace == CurrentNamespace;
        Namespace = ns;
        if (sameEndpoint) return;
        logger.LogInformation("Connecting to namespace {Namespace}", fullyQualifiedNamespace);
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
