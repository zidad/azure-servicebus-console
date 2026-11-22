using System.Text.Json.Serialization;

namespace ServiceBusConsole.Core.Services;

/// <summary>A broker the user opened, with the connection it was opened from. The JSON names are the cached ones from before the rename.</summary>
public sealed record RecentBroker(
    [property: JsonPropertyName("Subscription")] ConnectionInfo Connection,
    [property: JsonPropertyName("Namespace")] BrokerInfo Broker,
    DateTimeOffset Opened)
{
    public bool Is(ConnectionInfo connection, BrokerInfo broker) =>
        Connection.Id == connection.Id && Broker.Address == broker.Address;
}
