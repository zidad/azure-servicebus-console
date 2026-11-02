using System.Text;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Plugins.Linear.Tests;

/// <summary>An action context for a dead-lettered order, with the plugin's defaults under <c>settings</c>.</summary>
internal static class LinearTestContext
{
    public static readonly MessageInfo Message = new()
    {
        SequenceNumber = 42,
        State = MessageState.Peeked,
        MessageId = "msg-1",
        Subject = "OrderPlaced",
        ContentType = "application/json",
        Body = Encoding.UTF8.GetBytes("""{"orderId":7}"""),
        DeadLetterReason = "MaxDeliveryCountExceeded",
    };

    public static readonly MessageSource Source = new MessageSource.Queue("orders", IsDlq: true);

    public static MessageActionContext Create(IReadOnlyDictionary<string, string?> settings, MessageInfo? message = null, IProgress<string>? progress = null) => new()
    {
        PluginId = LinearPlugin.PluginId,
        Message = message ?? Message,
        Source = Source,
        Session = new SessionSnapshot
        {
            At = DateTimeOffset.UnixEpoch,
            Namespace = new NamespaceInfo { Name = "acme-prod", FullyQualifiedNamespace = "acme-prod.servicebus.windows.net", ResourceGroup = "rg", Location = "westeurope" },
            Source = Source,
            Mode = "Peek",
            Message = message ?? Message,
            Activity = [new ActivityEntry(DateTimeOffset.UnixEpoch, ActivityKind.Navigated, "Opened orders DLQ", Source)],
        },
        Settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new LinearPlugin().Settings.Where(s => s.Default is not null).Select(s => KeyValuePair.Create(s.Key, s.Default)))
            .AddInMemoryCollection(settings)
            .Build(),
        Host = new NoPluginHost(),
        Progress = progress ?? new RecordingProgress(),
    };
}
