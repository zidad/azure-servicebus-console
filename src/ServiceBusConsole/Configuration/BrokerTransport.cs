using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Configuration;

/// <summary>What an app built on this UI adds for its broker: the settings it reads and its services (<see cref="IConnectionCatalog"/>, <see cref="IBrokerOpener"/>).</summary>
public sealed record BrokerTransport(IReadOnlyList<ConfigKey> Settings, Action<IServiceCollection, IConfiguration> AddServices)
{
    /// <summary>No broker: for tests of commands that never open one.</summary>
    public static BrokerTransport None { get; } = new([], (_, _) => { });
}
