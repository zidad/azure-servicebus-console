namespace ServiceBusConsole.Abstractions;

/// <summary>A metric's value for the interval starting at <paramref name="Time"/>.</summary>
public readonly record struct MetricPoint(DateTimeOffset Time, double Value);
