using EasyNetQ.Management.Client;
using Microsoft.Extensions.Logging;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>Opens a virtual host (<see cref="BrokerInfo.Name"/>), with services wrapped by the registered decorators.</summary>
internal sealed class VhostOpener(
    RabbitMqOptions options, IManagementClient management, ILoggerFactory loggers, IEnumerable<IBrokerDecorator> decorators) : IBrokerOpener
{
    public IBroker Open(BrokerInfo info)
    {
        loggers.CreateLogger<VhostOpener>().LogInformation("Opening virtual host {Vhost} on {Server}", info.Name, options.Management);
        var amqp = new AmqpConnection(options, info.Name);
        IQueueService queues = new QueueService(management, info.Name, loggers.CreateLogger<QueueService>());
        return new VhostBroker(
            info,
            amqp,
            decorators.Aggregate(queues, (service, decorator) => decorator.Decorate(info, service)),
            new MessageService(amqp, loggers.CreateLogger<MessageService>()));
    }
}
