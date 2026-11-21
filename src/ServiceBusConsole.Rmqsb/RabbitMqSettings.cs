using Microsoft.Extensions.Configuration;
using ServiceBusConsole.RabbitMQ;

namespace ServiceBusConsole.Rmqsb;

/// <summary>The <see cref="RabbitMqOptions"/> that the <see cref="RabbitMqTransport"/> keys set.</summary>
public static class RabbitMqSettings
{
    /// <exception cref="CommandFailure">A URL isn't one with the scheme its key needs (usage exit code).</exception>
    public static RabbitMqOptions Read(IConfiguration configuration) => new()
    {
        Management = Url(configuration, RabbitMqTransport.Management, "http", "https"),
        Amqp = Url(configuration, RabbitMqTransport.Amqp, "amqp", "amqps"),
        User = Value(configuration, RabbitMqTransport.User),
        Password = Value(configuration, RabbitMqTransport.Password),
    };

    private static Uri Url(IConfiguration configuration, ConfigKey key, params string[] schemes) =>
        Value(configuration, key) is var value
        && Uri.TryCreate(value, UriKind.Absolute, out var uri) && schemes.Contains(uri.Scheme)
            ? uri
            : throw new CommandFailure(ExitCodes.Usage, Strings.FormatRabbitMqSettings_InvalidUrl(value, key.Key, string.Join(" | ", schemes)));

    private static string Value(IConfiguration configuration, ConfigKey key) => (configuration[key.ConfigurationPath] ?? key.Default ?? "").Trim();
}
