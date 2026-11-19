namespace ServiceBusConsole.Abstractions;

/// <summary>A broker field only some transports have (Service Bus: SKU, region, resource group), shown beside its name.</summary>
/// <param name="Id">Its name in JSON: <c>sku</c>.</param>
/// <param name="Label">What the UI calls it; a column header.</param>
/// <param name="Listed">A column in the connection list; false for long values such as a resource ID.</param>
/// <param name="Healthy">For a status: whether it is the healthy one, which the UI marks.</param>
public sealed record BrokerProperty(string Id, string Label, string Value, bool Listed = true, bool? Healthy = null);
