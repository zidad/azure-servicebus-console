using RabbitMQ.Client;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>One AMQP connection to a virtual host, opened on first use and again after it closed; channels per operation.</summary>
internal sealed class AmqpConnection(RabbitMqOptions options, string vhost) : IAsyncDisposable
{
    private readonly SemaphoreSlim _gate = new(1);
    private IConnection? _connection;

    // The client writes the current trace context into every publish: a copy would get this console's instead of its own.
    static AmqpConnection() => RabbitMQActivitySource.ContextInjector = (_, _) => { };

    public string Vhost => vhost;

    /// <param name="confirms">Publishes wait for the broker's confirm, and throw when it nacks or returns them.</param>
    public async Task<IChannel> OpenChannelAsync(bool confirms, CancellationToken ct)
    {
        var connection = await ConnectionAsync(ct);
        return await connection.CreateChannelAsync(new CreateChannelOptions(confirms, confirms), ct);
    }

    private async Task<IConnection> ConnectionAsync(CancellationToken ct)
    {
        await _gate.WaitAsync(ct);
        try
        {
            if (_connection is { IsOpen: true } open) return open;
            if (_connection is { } closed) await closed.DisposeAsync();
            return _connection = await Factory().CreateConnectionAsync(ct);
        }
        finally
        {
            _gate.Release();
        }
    }

    // The URI's own user, password and virtual host are replaced by the configured ones.
    private ConnectionFactory Factory() => new()
    {
        Uri = options.Amqp,
        VirtualHost = vhost,
        UserName = options.User,
        Password = options.Password,
        ClientProvidedName = "ServiceBusConsole",
        AutomaticRecoveryEnabled = false,
    };

    public async ValueTask DisposeAsync()
    {
        if (_connection is { } connection) await connection.DisposeAsync();
        _gate.Dispose();
    }
}
