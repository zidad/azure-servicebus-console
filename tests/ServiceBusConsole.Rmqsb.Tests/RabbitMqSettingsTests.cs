using Microsoft.Extensions.Configuration;
using ServiceBusConsole.RabbitMQ;

namespace ServiceBusConsole.Rmqsb.Tests;

public sealed class RabbitMqSettingsTests
{
    private static RabbitMqOptions Read(params (string Key, string Value)[] settings) => RabbitMqSettings.Read(
        new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value))).Build());

    [Test]
    public void Unset_IsGuestOnLocalhost() =>
        Read().ShouldBe(new RabbitMqOptions
        {
            Management = new("http://localhost:15672"), Amqp = new("amqp://localhost:5672"), User = "guest", Password = "guest",
        });

    [Test]
    public void Values_AreTrimmed() =>
        Read(("rabbitmq:management", " https://rabbit.example.com/admin/ "), ("rabbitmq:amqp", "amqps://rabbit.example.com"), ("rabbitmq:user", " ci "))
            .ShouldBe(new RabbitMqOptions
            {
                Management = new("https://rabbit.example.com/admin/"), Amqp = new("amqps://rabbit.example.com"), User = "ci", Password = "guest",
            });

    [Test]
    [Arguments("rabbitmq:management", "amqp://localhost", "amqp://localhost is not a valid rabbitmq.management: expected a http | https URL")]
    [Arguments("rabbitmq:amqp", "localhost:5672", "localhost:5672 is not a valid rabbitmq.amqp: expected a amqp | amqps URL")]
    public void InvalidUrl_IsAUsageError(string key, string value, string message)
    {
        var failure = Should.Throw<CommandFailure>(() => Read((key, value)));

        failure.ExitCode.ShouldBe(ExitCodes.Usage);
        failure.Message.ShouldBe(message);
    }

    [Test]
    public void Password_IsListedMasked() => RabbitMqTransport.Password.IsSecret.ShouldBeTrue();
}
