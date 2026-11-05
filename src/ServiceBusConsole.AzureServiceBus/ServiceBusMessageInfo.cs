using System.Text;
using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus;

/// <summary>A <see cref="MessageInfo"/> that keeps the received message, so copies carry everything it had.</summary>
internal sealed record ServiceBusMessageInfo : MessageInfo
{
    // Service Bus keeps both in the application properties too.
    private static readonly HashSet<string> DeadLetterProperties = ["DeadLetterReason", "DeadLetterErrorDescription"];

    public required ServiceBusReceivedMessage Raw { get; init; }

    public static ServiceBusMessageInfo From(ServiceBusReceivedMessage raw, MessageState state) => new()
    {
        Raw = raw,
        State = state,
        SequenceNumber = raw.SequenceNumber,
        MessageId = raw.MessageId ?? "",
        EnqueuedTime = raw.EnqueuedTime,
        ContentType = raw.ContentType ?? "",
        Subject = raw.Subject ?? "",
        Body = BodyOf(raw),
        DeliveryCount = raw.DeliveryCount,
        DeadLetterReason = raw.DeadLetterReason,
        DeadLetterErrorDescription = raw.DeadLetterErrorDescription,
        Properties = [.. raw.ApplicationProperties
            .Where(p => !DeadLetterProperties.Contains(p.Key))
            .Select(p => KeyValuePair.Create(p.Key, p.Value?.ToString() ?? ""))],
    };

    public static ServiceBusReceivedMessage RawOf(MessageInfo message) =>
        message is ServiceBusMessageInfo { Raw: var raw }
            ? raw
            : throw new ArgumentException($"Message #{message.SequenceNumber} was not received from Service Bus.", nameof(message));

    // Body throws for AMQP value and sequence bodies (e.g. from JMS clients); a value is shown as text.
    private static ReadOnlyMemory<byte> BodyOf(ServiceBusReceivedMessage raw) => raw.GetRawAmqpMessage().Body switch
    {
        var body when body.BodyType == AmqpMessageBodyType.Data => raw.Body.ToMemory(),
        var body when body.TryGetValue(out var value) => Encoding.UTF8.GetBytes($"{value}"),
        _ => ReadOnlyMemory<byte>.Empty,
    };
}
