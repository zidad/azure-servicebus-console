using System.Text;

namespace ServiceBusConsole.Abstractions;

/// <summary>
/// A received or peeked message and how this console holds it. Not sealed: a service derives from it to
/// keep its own message, so it can send exact copies.
/// </summary>
public record MessageInfo
{
    public required long SequenceNumber { get; init; }
    public required MessageState State { get; init; }
    public string MessageId { get; init; } = "";
    public DateTimeOffset EnqueuedTime { get; init; }
    public string ContentType { get; init; } = "";
    public string Subject { get; init; } = "";
    public ReadOnlyMemory<byte> Body { get; init; }
    public int DeliveryCount { get; init; }
    public string? DeadLetterReason { get; init; }

    /// <summary>Application properties as text, without the <see cref="DeadLetterReason"/>.</summary>
    public IReadOnlyList<KeyValuePair<string, string>> Properties { get; init; } = [];

    public string BodyText => Encoding.UTF8.GetString(Body.Span);
}
