using System.Text;
using RabbitMQ.Client;

namespace ServiceBusConsole.RabbitMQ.Tests;

public sealed class RabbitMqMessageInfoTests
{
    private static byte[] Utf8(string text) => Encoding.UTF8.GetBytes(text);

    private static BasicGetResult Result(BasicProperties properties, ulong tag = 7, bool redelivered = false) =>
        new(tag, redelivered, "orders", "orders.created", 0, properties, Utf8("{\"id\":1}"));

    private static readonly Dictionary<string, object?> Death = new()
    {
        ["reason"] = Utf8("rejected"), ["queue"] = Utf8("orders"), ["count"] = 3L, ["exchange"] = Utf8("orders"),
    };

    [Test]
    public void Maps_TheAmqpProperties()
    {
        var message = RabbitMqMessageInfo.From(Result(new BasicProperties
        {
            MessageId = "order-1", ContentType = "application/json", Type = "order-created", CorrelationId = "c-1",
            Timestamp = new AmqpTimestamp(1_700_000_000),
        }, redelivered: true), MessageState.Peeked);

        message.Key.ShouldBe(new MessageKey("7"));
        message.MessageId.ShouldBe("order-1");
        message.ContentType.ShouldBe("application/json");
        message.Subject.ShouldBe("order-created");
        message.EnqueuedTime.ShouldBe(DateTimeOffset.FromUnixTimeSeconds(1_700_000_000));
        message.BodyText.ShouldBe("{\"id\":1}");
        message.TransportProperties.Select(p => (p.Id, p.Text)).ShouldBe(
            [("exchange", "orders"), ("routingKey", "orders.created"), ("redelivered", "true"), ("correlationId", "c-1")]);
    }

    [Test]
    public void Headers_AreText_WithoutWhatTheBrokerAndClientStamp()
    {
        var message = RabbitMqMessageInfo.From(Result(new BasicProperties
        {
            Headers = new Dictionary<string, object?>
            {
                ["source"] = Utf8("seed"), ["attempt"] = 2, ["tags"] = new List<object?> { Utf8("a"), Utf8("b") },
                ["x-delivery-count"] = 4L, ["x-acquired-count"] = 5L, ["x-dotnet-pub-seq-no"] = 9UL, ["x-death"] = new List<object?> { Death },
            },
        }), MessageState.Peeked);

        message.Properties.ShouldBe([new("source", "seed"), new("attempt", "2"), new("tags", "[a, b]")]);
        message.DeliveryCount.ShouldBe(4);
    }

    [Test]
    public void TheLatestDeath_IsTheDeadLetterReason()
    {
        var earlier = new Dictionary<string, object?> { ["reason"] = Utf8("expired"), ["queue"] = Utf8("payments"), ["count"] = 1L };
        var message = RabbitMqMessageInfo.From(Result(new BasicProperties
        {
            Headers = new Dictionary<string, object?> { ["x-death"] = new List<object?> { Death, earlier } },
        }), MessageState.Locked);

        message.DeadLetterReason.ShouldBe("rejected");
        message.DeadLetterErrorDescription.ShouldBe("Dead-lettered from queue orders, 3 times");
    }

    [Test]
    public void NoDeath_NoReason()
    {
        var message = RabbitMqMessageInfo.From(Result(new BasicProperties()), MessageState.Peeked);

        message.DeadLetterReason.ShouldBeNull();
        message.DeadLetterErrorDescription.ShouldBeNull();
        message.DeliveryCount.ShouldBe(0);
    }

    [Test]
    public void ACopy_KeepsTheProperties_AndDropsTheStampedHeaders()
    {
        var message = RabbitMqMessageInfo.From(Result(new BasicProperties
        {
            MessageId = "order-1", Type = "order-created", DeliveryMode = DeliveryModes.Persistent,
            Headers = new Dictionary<string, object?> { ["source"] = Utf8("seed"), ["x-delivery-count"] = 4L, ["x-death"] = new List<object?> { Death } },
        }), MessageState.Peeked);

        var copy = RabbitMqMessageInfo.CopyOf(message);

        copy.MessageId.ShouldBe("order-1");
        copy.Type.ShouldBe("order-created");
        copy.DeliveryMode.ShouldBe(DeliveryModes.Persistent);
        copy.Headers.ShouldNotBeNull().Keys.ShouldBe(["source", "x-death"]);
    }

    [Test]
    public void ACopy_NeedsAMessageFromRabbitMq() =>
        Should.Throw<ArgumentException>(() => RabbitMqMessageInfo.CopyOf(new MessageInfo { Key = new("1"), State = MessageState.Peeked }));
}
