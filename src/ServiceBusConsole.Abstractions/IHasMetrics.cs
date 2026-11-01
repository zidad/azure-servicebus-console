namespace ServiceBusConsole.Abstractions;

/// <summary>A namespace with Azure Monitor metrics; the local emulator has none.</summary>
public interface IHasMetrics
{
    IMetricsService Metrics { get; }
}
