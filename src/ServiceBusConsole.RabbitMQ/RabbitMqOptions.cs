namespace ServiceBusConsole.RabbitMQ;

/// <summary>Where <see cref="ServiceCollectionExtensions.AddRabbitMq"/> connects; register one to change the defaults.</summary>
public sealed record RabbitMqOptions
{
    /// <summary>The management plugin's HTTP API: virtual hosts, queues and their counts.</summary>
    public Uri Management { get; init; } = new("http://localhost:15672");

    /// <summary>The AMQP listener: messages, in the virtual host being browsed.</summary>
    public Uri Amqp { get; init; } = new("amqp://localhost:5672");

    public string User { get; init; } = "guest";

    public string Password { get; init; } = "guest";
}
