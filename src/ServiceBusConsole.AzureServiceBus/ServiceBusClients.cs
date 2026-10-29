using Azure.Identity;
using Azure.Messaging.ServiceBus;
using Azure.Messaging.ServiceBus.Administration;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>Clients for the namespace <see cref="ServiceBusConnection"/> points at, created when it changes.</summary>
internal sealed class ServiceBusClients(ServiceBusConnection connection, AzureCliCredential credential, ILogger<ServiceBusClients> logger)
{
    private const string EmulatorConnectionString =
        "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;";

    private readonly Lock _sync = new();
    private Clients? _clients;

    // The emulator has no management API.
    private sealed record Clients(string Namespace, ServiceBusAdministrationClient? Admin, ServiceBusClient Bus);

    public ServiceBusAdministrationClient Admin =>
        Current().Admin ?? throw new InvalidOperationException("Management API is not supported by the local emulator.");

    public ServiceBusClient Bus => Current().Bus;

    private Clients Current()
    {
        var ns = connection.CurrentNamespace ?? throw new InvalidOperationException("No namespace selected.");
        lock (_sync) return _clients?.Namespace == ns ? _clients : _clients = Create(ns);
    }

    private Clients Create(string ns)
    {
        logger.LogInformation("Connecting to namespace {Namespace}", ns);
        return ns == NamespaceInfo.Emulator.FullyQualifiedNamespace
            ? new(ns, null, new ServiceBusClient(EmulatorConnectionString))
            : new(ns, new ServiceBusAdministrationClient($"https://{ns}", credential), new ServiceBusClient($"sb://{ns}/", credential));
    }
}
