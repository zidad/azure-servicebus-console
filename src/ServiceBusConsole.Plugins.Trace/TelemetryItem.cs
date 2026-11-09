namespace ServiceBusConsole.Plugins.Trace;

/// <summary>One Application Insights record of a trace: what the end-to-end transaction view opens on.</summary>
public sealed record TelemetryItem(string ItemId, string Timestamp, string Table);
