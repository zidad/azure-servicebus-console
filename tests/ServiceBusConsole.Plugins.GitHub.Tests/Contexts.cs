using System.Text;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Plugins.GitHub.Tests;

internal static class Contexts
{
    public static readonly MessageInfo Message = new()
    {
        Key = MessageKey.From(42), TransportProperties = [SequenceNumber(42)],
        State = MessageState.Peeked,
        MessageId = "msg-42",
        Subject = "order-created",
        DeadLetterReason = "MaxDeliveryCountExceeded",
        Body = Encoding.UTF8.GetBytes("""{"orderId":7}"""),
        Properties = [new("tenant", "contoso")],
    };

    public static readonly MessageSource Source = new MessageSource.Queue("orders", IsDlq: true);

    public static readonly SessionSnapshot Session = new()
    {
        At = DateTimeOffset.UnixEpoch,
        Broker = new BrokerInfo
        {
            Name = "sb-orders-dev", Address = "sb-orders-dev.servicebus.windows.net", Properties = ServiceBusProperties(sku: "Standard", location: "westeurope", resourceGroup: "rg-orders"),
        },
        Source = Source,
        Mode = "Peek",
        Message = Message,
        Activity = [new ActivityEntry(DateTimeOffset.UnixEpoch, ActivityKind.Loaded, "Peeked 10 messages", Source)],
    };

    public static MessageActionContext Create(IPluginHost host, MessageInfo? message = null, params (string Key, string? Value)[] settings) => new()
    {
        PluginId = GitHubPlugin.PluginId,
        Message = message ?? Message,
        Source = Source,
        Session = Session,
        Settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new GitHubPlugin().Settings.Select(s => KeyValuePair.Create(s.Key, s.Default)))
            .AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value)))
            .Build(),
        Host = host,
        Progress = new Progress<string>(),
    };
}
