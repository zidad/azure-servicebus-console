using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Configuration;

/// <summary>
/// What an app built on this UI adds for its broker: its words (<see cref="BrokerTerms"/>, used through <see cref="UI.Terms"/>),
/// the settings it reads and its services (<see cref="IConnectionCatalog"/>, <see cref="IBrokerOpener"/>).
/// </summary>
public sealed record BrokerTransport(BrokerTerms Terms, IReadOnlyList<ConfigKey> Settings, Action<IServiceCollection, IConfiguration> AddServices)
{
    /// <summary>No broker: for tests of commands that never open one.</summary>
    public static BrokerTransport None { get; } = new(BrokerTerms.Generic, [], (_, _) => { });
}
