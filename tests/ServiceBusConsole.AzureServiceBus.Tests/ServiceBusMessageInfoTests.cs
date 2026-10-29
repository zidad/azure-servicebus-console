using Azure.Core.Amqp;
using Azure.Messaging.ServiceBus;

namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class ServiceBusMessageInfoTests
{
    [Fact]
    public void From_ShowsTheDeadLetterReasonApartFromTheProperties()
    {
        var raw = ServiceBusModelFactory.ServiceBusReceivedMessage(
            body: BinaryData.FromString("{}"), messageId: "m-7", sequenceNumber: 7, deliveryCount: 3,
            properties: new Dictionary<string, object> { ["DeadLetterReason"] = "MaxDeliveryCountExceeded", ["attempt"] = 3 });

        var message = ServiceBusMessageInfo.From(raw, MessageState.Peeked);

        message.SequenceNumber.ShouldBe(7);
        message.MessageId.ShouldBe("m-7");
        message.BodyText.ShouldBe("{}");
        message.DeliveryCount.ShouldBe(3);
        message.DeadLetterReason.ShouldBe("MaxDeliveryCountExceeded");
        message.Properties.ShouldBe([KeyValuePair.Create("attempt", "3")]);
        ServiceBusMessageInfo.RawOf(message).ShouldBeSameAs(raw);
    }

    [Fact]
    public void From_ShowsAnAmqpValueBodyAsText()
    {
        // ServiceBusReceivedMessage.Body throws for these, e.g. a JMS text message.
        var raw = ServiceBusReceivedMessage.FromAmqpMessage(
            new AmqpAnnotatedMessage(AmqpMessageBody.FromValue("hello")) { Header = { DeliveryCount = 1 } }, BinaryData.FromString("lock"));

        ServiceBusMessageInfo.From(raw, MessageState.Peeked).BodyText.ShouldBe("hello");
    }

    [Fact]
    public void RawOf_RejectsAMessageThatDidNotComeFromServiceBus() =>
        Should.Throw<ArgumentException>(() => ServiceBusMessageInfo.RawOf(new MessageInfo { SequenceNumber = 1, State = MessageState.Peeked }));
}
