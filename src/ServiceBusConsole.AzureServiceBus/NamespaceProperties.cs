using ServiceBusConsole.AzureServiceBus.Resources;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>A Service Bus namespace's <see cref="BrokerInfo.Properties"/>: the known ones, by id.</summary>
internal static class NamespaceProperties
{
    public const string Sku = "sku", Location = "location", ResourceGroup = "resourceGroup", Status = "status", ResourceId = "resourceId";

    public static IReadOnlyList<BrokerProperty> Of(string? sku, string? location, string? resourceGroup, string? status, string? resourceId) =>
    [
        .. new[]
        {
            Known(Sku, Strings.Namespace_Sku, sku),
            Known(Location, Strings.Namespace_Region, location),
            Known(ResourceGroup, Strings.Namespace_ResourceGroup, resourceGroup),
            Known(Status, Strings.Namespace_Status, status) is { } s ? s with { Healthy = s.Value == "Active" } : null,
            Known(ResourceId, Strings.Namespace_ResourceId, resourceId) is { } id ? id with { Listed = false } : null,
        }.OfType<BrokerProperty>(),
    ];

    private static BrokerProperty? Known(string id, string label, string? value) => string.IsNullOrEmpty(value) ? null : new(id, label, value);
}
