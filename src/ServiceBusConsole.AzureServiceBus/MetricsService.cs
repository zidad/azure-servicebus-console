using System.Runtime.CompilerServices;
using Azure.Core;
using Azure.ResourceManager;
using Azure.ResourceManager.Monitor;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.AzureServiceBus;

internal sealed class MetricsService(ServiceBusConnection connection, ArmClient armClient, ILogger<MetricsService> logger) : IMetricsService
{
    public async IAsyncEnumerable<MessageMetrics> GetMetricsAsync(MetricsScope scope, MetricsWindow window, [EnumeratorCancellation] CancellationToken ct = default)
    {
        var ns = connection.Namespace ?? throw new InvalidOperationException("No namespace selected.");
        if (connection.IsEmulator) throw new NotSupportedException("The local emulator has no Azure Monitor metrics.");
        var resource = new ResourceIdentifier(ns.ResourceId
            ?? throw new InvalidOperationException("Resource ID of the namespace unknown; reopen it from the connection page."));

        logger.LogInformation("Loading metrics of {Scope} in {Namespace}", scope, ns.Name);
        var results = await Task.WhenAll(MonitorMetrics.Queries(scope, window)
            .Select(query => armClient.GetMonitorMetricsAsync(resource, query, ct).ToListAsync(ct).AsTask()));

        var metrics = results.SelectMany(r => r).ToList();
        if (metrics.FirstOrDefault(m => m.ErrorCode is { } code && code != "Success") is { } failed)
            throw new InvalidOperationException(failed.ErrorMessage ?? failed.ErrorCode);
        yield return MonitorMetrics.ToMessageMetrics(window, metrics);
    }
}
