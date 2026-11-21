namespace ServiceBusConsole.RabbitMQ;

/// <summary>
/// A virtual host: queues and their messages. A peek takes messages and puts them back (<see cref="PeekKind.Requeues"/>);
/// no topics, dead-letter queues of its own, metrics or deletion yet.
/// </summary>
internal sealed class VhostBroker(BrokerInfo info, AmqpConnection amqp, IQueueService queues, MessageService messages) : IBroker, IHasPeek
{
    public BrokerInfo Info => info;
    public IQueueService Queues => queues;
    public IMessageService Messages => messages;
    public IPeekService Peek => messages;

    public ValueTask DisposeAsync() => amqp.DisposeAsync();
}
