using System.Collections;
using System.Globalization;
using System.Text;
using System.Text.Json.Nodes;
using RabbitMQ.Client;

namespace ServiceBusConsole.RabbitMQ;

/// <summary>
/// A <see cref="MessageInfo"/> that keeps the AMQP properties it arrived with, so copies carry everything it had.
/// Its key is the delivery tag: unique among the messages of one peek or receive.
/// </summary>
internal sealed record RabbitMqMessageInfo : MessageInfo
{
    // Set by the broker: the quorum queue delivery count, and the dead-letter history shown as the reason.
    private const string DeliveryCountHeader = "x-delivery-count";
    private const string DeathHeader = "x-death";

    // Set again on every copy: by the broker (quorum queues since 4.0), and by RabbitMQ.Client 7 for confirm tracking.
    private static readonly HashSet<string> Restamped = [DeliveryCountHeader, "x-acquired-count", "x-dotnet-pub-seq-no"];

    public required IReadOnlyBasicProperties Amqp { get; init; }

    public required ulong DeliveryTag { get; init; }

    public static RabbitMqMessageInfo From(BasicGetResult result, MessageState state)
    {
        var props = result.BasicProperties;
        var headers = props.Headers ?? new Dictionary<string, object?>();
        var death = Deaths(headers).FirstOrDefault();
        return new()
        {
            Amqp = props,
            DeliveryTag = result.DeliveryTag,
            Key = new(result.DeliveryTag.ToString(CultureInfo.InvariantCulture)),
            State = state,
            MessageId = props.MessageId ?? "",
            EnqueuedTime = props.IsTimestampPresent() ? DateTimeOffset.FromUnixTimeSeconds(props.Timestamp.UnixTime) : default,
            ContentType = props.ContentType ?? "",
            Subject = props.Type ?? "",
            Body = result.Body.ToArray(),
            DeliveryCount = headers.TryGetValue(DeliveryCountHeader, out var count) && count is long or int ? Convert.ToInt32(count, CultureInfo.InvariantCulture) : 0,
            DeadLetterReason = death is null ? null : Field(death, "reason"),
            DeadLetterErrorDescription = death is null ? null
                : Strings.FormatRabbitMqMessageInfo_DeadLettered(Field(death, "queue"), Field(death, "count")),
            Properties = [.. headers
                .Where(h => h.Key != DeathHeader && !Restamped.Contains(h.Key))
                .Select(h => KeyValuePair.Create(h.Key, Text(h.Value)))],
            TransportProperties = [.. TransportFields(result)],
        };
    }

    /// <summary>The properties a copy is published with: the original's, without what the broker sets itself.</summary>
    public static BasicProperties CopyOf(MessageInfo message)
    {
        var amqp = message is RabbitMqMessageInfo { Amqp: var original }
            ? original
            : throw new ArgumentException(Strings.FormatRabbitMqMessageInfo_NotReceived(message.Key), nameof(message));
        var copy = new BasicProperties(amqp);
        if (amqp.Headers is { } headers)
            copy.Headers = headers.Where(h => !Restamped.Contains(h.Key)).ToDictionary(h => h.Key, h => h.Value);
        return copy;
    }

    private static IEnumerable<TransportProperty> TransportFields(BasicGetResult result)
    {
        var props = result.BasicProperties;
        yield return new("exchange", Strings.RabbitMqMessageInfo_Exchange, JsonValue.Create(result.Exchange));
        yield return new("routingKey", Strings.RabbitMqMessageInfo_RoutingKey, JsonValue.Create(result.RoutingKey));
        yield return new("redelivered", Strings.RabbitMqMessageInfo_Redelivered, JsonValue.Create(result.Redelivered));
        if (props.CorrelationId is { } correlationId)
            yield return new("correlationId", Strings.RabbitMqMessageInfo_CorrelationId, JsonValue.Create(correlationId));
        if (props.ReplyTo is { } replyTo)
            yield return new("replyTo", Strings.RabbitMqMessageInfo_ReplyTo, JsonValue.Create(replyTo));
        if (props.AppId is { } appId)
            yield return new("appId", Strings.RabbitMqMessageInfo_AppId, JsonValue.Create(appId));
        if (props.IsPriorityPresent())
            yield return new("priority", Strings.RabbitMqMessageInfo_Priority, JsonValue.Create(props.Priority));
        if (props.Expiration is { } expiration)
            yield return new("expiration", Strings.RabbitMqMessageInfo_Expiration, JsonValue.Create(expiration));
    }

    // Most recent first, as the broker writes them.
    private static IEnumerable<IDictionary<string, object?>> Deaths(IDictionary<string, object?> headers) =>
        headers.TryGetValue(DeathHeader, out var deaths) && deaths is IEnumerable list
            ? list.OfType<IDictionary<string, object?>>()
            : [];

    private static string Field(IDictionary<string, object?> table, string name) => table.TryGetValue(name, out var value) ? Text(value) : "";

    /// <summary>A header value as text: AMQP strings arrive as UTF-8 bytes.</summary>
    private static string Text(object? value) => value switch
    {
        null => "",
        byte[] bytes => Encoding.UTF8.GetString(bytes),
        string text => text,
        AmqpTimestamp timestamp => DateTimeOffset.FromUnixTimeSeconds(timestamp.UnixTime).ToString("O", CultureInfo.InvariantCulture),
        IDictionary<string, object?> table => $"{{{string.Join(", ", table.Select(e => $"{e.Key}: {Text(e.Value)}"))}}}",
        IEnumerable list => $"[{string.Join(", ", list.Cast<object?>().Select(Text))}]",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture),
        _ => value.ToString() ?? "",
    };
}
