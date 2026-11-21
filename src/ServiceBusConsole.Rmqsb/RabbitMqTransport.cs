using Microsoft.Extensions.DependencyInjection;
using ServiceBusConsole.RabbitMQ;

namespace ServiceBusConsole.Rmqsb;

/// <summary>RabbitMQ as rmqsb's broker, with its <c>rabbitmq.*</c> settings.</summary>
public static class RabbitMqTransport
{
    public static ConfigKey Management { get; } = new("rabbitmq.management", Strings.RabbitMqTransport_Management, "http://localhost:15672");

    public static ConfigKey Amqp { get; } = new("rabbitmq.amqp", Strings.RabbitMqTransport_Amqp, "amqp://localhost:5672");

    public static ConfigKey User { get; } = new("rabbitmq.user", Strings.RabbitMqTransport_User, "guest");

    public static ConfigKey Password { get; } = new("rabbitmq.password", Strings.RabbitMqTransport_Password, "guest") { IsSecret = true };

    public static BrokerTransport Broker { get; } = new([Management, Amqp, User, Password], (services, configuration) => services
        .AddSingleton(_ => RabbitMqSettings.Read(configuration))
        .AddRabbitMq());
}
