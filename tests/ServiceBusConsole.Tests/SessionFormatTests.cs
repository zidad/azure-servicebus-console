using System.Text;
using System.Text.Json;

namespace ServiceBusConsole.Tests;

public sealed class SessionFormatTests
{
    private static readonly DateTimeOffset At = new(2026, 11, 2, 9, 30, 0, TimeSpan.Zero);

    private static readonly MessageSource OrdersDlq = new MessageSource.Queue("orders", IsDlq: true);
    private static readonly MessageSource Billing = new MessageSource.Subscription("orders", "billing");

    private static MessageInfo Message(string body, string? deadLetterReason = null) => new()
    {
        SequenceNumber = 42,
        State = MessageState.Peeked,
        MessageId = "msg-42",
        Subject = "order-created",
        ContentType = "application/json",
        EnqueuedTime = new DateTimeOffset(2026, 11, 1, 23, 5, 7, 250, TimeSpan.FromHours(1)),
        DeliveryCount = 3,
        DeadLetterReason = deadLetterReason,
        Body = Encoding.UTF8.GetBytes(body),
        Properties = [new("tenant", "contoso"), new("route", "a|b")],
    };

    private static SessionSnapshot Snapshot(MessageSource source, MessageInfo? message) => new()
    {
        At = At,
        Subscription = AppHarness.AzureSubscription,
        Namespace = AppHarness.AzureNamespace,
        Source = source,
        Mode = "Peek",
        Message = message,
        Activity =
        [
            new(At.AddMinutes(-2), ActivityKind.Navigated, "Opened /queues"),
            new(At.AddMinutes(-1), ActivityKind.Loaded, "Peeked 1 message(s)", source),
        ],
    };

    [Test]
    public void MessageMarkdown_ListsFieldsPropertiesAndPrettyJsonBody()
    {
        var markdown = MessageMarkdown.Format(Message("{\"id\":1,\"lines\":[\"a\"]}", "MaxDeliveryCountExceeded"));

        markdown.ShouldBe("""
            ## Message #42

            - Message ID: `msg-42`
            - Sequence number: 42
            - State: Peeked
            - Subject: order-created
            - Content type: application/json
            - Enqueued: 2026-11-01T23:05:07.250+01:00
            - Delivery count: 3
            - Dead-letter reason: MaxDeliveryCountExceeded

            ### Application properties

            | Name | Value |
            |---|---|
            | tenant | contoso |
            | route | a\|b |

            ### Body

            ```json
            {
              "id": 1,
              "lines": [
                "a"
              ]
            }
            ```

            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void MessageMarkdown_KeepsATextBodyAsIs_InAFenceItCannotClose()
    {
        var markdown = MessageMarkdown.Format(Message("not json ``` really"), headingLevel: 3);

        markdown.ShouldStartWith("### Message #42\n");
        markdown.ShouldContain("#### Body\n\n````\nnot json ``` really\n````\n");
        markdown.ShouldNotContain("Dead-letter reason");
    }

    [Test]
    public void MessageMarkdown_SaysWhenThereAreNoPropertiesOrBody()
    {
        var markdown = MessageMarkdown.Format(Message("") with { Properties = [] });

        markdown.ShouldContain("### Application properties\n\nNone.\n");
        markdown.ShouldEndWith("### Body\n\nEmpty.\n");
    }

    [Test]
    public void SessionMarkdown_ShowsWhereTheMessageAndActivityNewestLast()
    {
        var markdown = Snapshot(OrdersDlq, Message("{}")).ToMarkdown();

        markdown.ShouldStartWith("""
            # Azure Service Bus session

            Captured 2026-11-02T09:30:00.000+00:00.

            ## Where

            - Azure subscription: Contoso Development (`6f1d2c3b-0000-4000-8000-000000000001`)
            - Namespace: sb-orders-dev (`sb-orders-dev.servicebus.windows.net`), Standard, West Europe, resource group rg-messaging-dev
            - Queue: `orders`, dead-letter queue
            - Mode: Peek

            ## Message #42

            """.ReplaceLineEndings("\n"));
        markdown.ShouldEndWith("""
            ## Recent activity

            - 2026-11-02T09:28:00.000+00:00 Navigated: Opened /queues
            - 2026-11-02T09:29:00.000+00:00 Loaded (orders DLQ): Peeked 1 message(s)

            """.ReplaceLineEndings("\n"));
    }

    [Test]
    public void SessionMarkdown_NamesATopicSubscription()
    {
        var markdown = Snapshot(Billing, message: null).ToMarkdown();

        markdown.ShouldContain("- Topic subscription: `billing` of topic `orders`\n");
        markdown.ShouldContain("Loaded (orders/billing): Peeked");
        markdown.ShouldNotContain("## Message");
    }

    [Test]
    public void SessionMarkdown_WithNothingOpen_SaysSo()
    {
        var markdown = new SessionSnapshot { At = At }.ToMarkdown();

        markdown.ShouldContain("## Where\n\nNot connected.\n");
        markdown.ShouldEndWith("## Recent activity\n\nNone.\n");
    }

    [Test]
    public void SessionJson_EmbedsAJsonBodyAsJson()
    {
        using var doc = JsonDocument.Parse(Snapshot(OrdersDlq, Message("{\"id\":1}", "MaxDeliveryCountExceeded")).ToJson());
        var json = doc.RootElement;

        json.GetProperty("at").GetString().ShouldBe("2026-11-02T09:30:00.000+00:00");
        json.GetProperty("subscription").GetProperty("id").GetString().ShouldBe(AppHarness.AzureSubscription.Id);
        json.GetProperty("namespace").GetProperty("fullyQualifiedNamespace").GetString().ShouldBe("sb-orders-dev.servicebus.windows.net");
        Compact(json.GetProperty("source")).ShouldBe("""{"kind":"queue","queue":"orders","isDlq":true,"displayName":"orders DLQ"}""");
        json.GetProperty("mode").GetString().ShouldBe("Peek");
        var message = json.GetProperty("message");
        message.GetProperty("sequenceNumber").GetInt64().ShouldBe(42);
        message.GetProperty("deadLetterReason").GetString().ShouldBe("MaxDeliveryCountExceeded");
        Compact(message.GetProperty("properties")).ShouldBe("""{"tenant":"contoso","route":"a|b"}""");
        message.GetProperty("body").GetProperty("id").GetInt32().ShouldBe(1);
        var activity = json.GetProperty("activity");
        activity.EnumerateArray().Select(a => a.GetProperty("kind").GetString()).ShouldBe(["Navigated", "Loaded"]);
        activity[0].GetProperty("source").ValueKind.ShouldBe(JsonValueKind.Null);
        activity[1].GetProperty("source").GetProperty("displayName").GetString().ShouldBe("orders DLQ");
    }

    [Test]
    public void SessionJson_KeepsATextBodyAsAString_AndNamesASubscription()
    {
        using var doc = JsonDocument.Parse(Snapshot(Billing, Message("{ broken")).ToJson());
        var json = doc.RootElement;

        Compact(json.GetProperty("source")).ShouldBe("""{"kind":"subscription","topic":"orders","subscription":"billing","isDlq":false,"displayName":"orders/billing"}""");
        json.GetProperty("message").GetProperty("body").GetString().ShouldBe("{ broken");
        json.GetProperty("message").GetProperty("deadLetterReason").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Test]
    public void MessageJson_FormatsTheMessageAlone()
    {
        using var doc = JsonDocument.Parse(MessageJson.Format(Message("plain text")));

        doc.RootElement.GetProperty("messageId").GetString().ShouldBe("msg-42");
        doc.RootElement.GetProperty("enqueuedTime").GetString().ShouldBe("2026-11-01T23:05:07.250+01:00");
        doc.RootElement.GetProperty("body").GetString().ShouldBe("plain text");
    }

    [Test]
    public void MessageJson_EmbedsJsonSentAsAnAmqpString_AndKeepsAPlainString()
    {
        using var wrapped = JsonDocument.Parse(MessageJson.Format(Message("\"{\\\"orderId\\\":\\\"ORD-1\\\"}\"")));
        using var plain = JsonDocument.Parse(MessageJson.Format(Message("\"just text\"")));

        wrapped.RootElement.GetProperty("body").GetProperty("orderId").GetString().ShouldBe("ORD-1");
        plain.RootElement.GetProperty("body").GetString().ShouldBe("\"just text\"");
    }

    [Test]
    public void SessionJson_IsIndentedWithUnescapedText()
    {
        var json = new SessionSnapshot { At = At, Activity = [new(At, ActivityKind.Error, "Can't reach <café>")] }.ToJson();

        json.ShouldStartWith("{\n  \"at\": \"2026-11-02T09:30:00.000+00:00\",\n  \"subscription\": null,");
        json.ShouldContain("\"text\": \"Can't reach <café>\"");
    }

    private static string Compact(JsonElement element) => JsonSerializer.Serialize(element);
}
