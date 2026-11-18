using System.Globalization;
using System.Text.Json.Nodes;

namespace ServiceBusConsole.Tests;

internal static class TestMessages
{
    /// <summary>The sequence number as the Service Bus transport adds it.</summary>
    public static TransportProperty SequenceNumber(long number) => new("sequenceNumber", "Sequence #", JsonValue.Create(number));

    /// <summary>A Service Bus key read back as its sequence number.</summary>
    public static long Number(this MessageInfo message) => long.Parse(message.Key.Value, CultureInfo.InvariantCulture);
}
