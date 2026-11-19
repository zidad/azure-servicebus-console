namespace ServiceBusConsole.Tests;

internal static class TestBrokers
{
    /// <summary>A namespace's properties as the Service Bus transport lists them.</summary>
    public static IReadOnlyList<BrokerProperty> ServiceBusProperties(
        string? sku = null, string? location = null, string? resourceGroup = null, string? status = null, string? resourceId = null) =>
    [
        .. new[]
        {
            sku is null ? null : new BrokerProperty("sku", "SKU", sku),
            location is null ? null : new BrokerProperty("location", "Region", location),
            resourceGroup is null ? null : new BrokerProperty("resourceGroup", "Resource group", resourceGroup),
            status is null ? null : new BrokerProperty("status", "Status", status, Healthy: status == "Active"),
            resourceId is null ? null : new BrokerProperty("resourceId", "Resource ID", resourceId, Listed: false),
        }.OfType<BrokerProperty>(),
    ];
}
