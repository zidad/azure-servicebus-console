namespace ServiceBusConsole.Abstractions;

/// <summary>
/// A namespace being browsed: what every namespace can do. Optional features are interfaces it implements or not
/// (<see cref="IHasTopics"/>, <see cref="IHasMetrics"/>, <see cref="IHasDeletion"/>). Owns its connections; disposing
/// it closes them.
/// </summary>
public interface IServiceBusNamespace : IAsyncDisposable
{
    NamespaceInfo Info { get; }
    IQueueService Queues { get; }
    IMessageService Messages { get; }
}
