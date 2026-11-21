using RabbitMQ.Client;

namespace ServiceBusConsole.IntegrationTests;

/// <summary>The RabbitMQ transport against a real broker: virtual hosts and queues over the management API, messages over AMQP.</summary>
[ClassDataSource<Brokers>(Shared = SharedType.PerTestSession)]
public sealed class RabbitMqTests(Brokers brokers)
{
    [Before(Test)]
    public void RequireBrokers() =>
        Skip.Unless(Brokers.Enabled, $"Needs Docker: set {Brokers.EnableVariable}=1 to run the RabbitMQ tests.");

    [Test]
    public async Task TheServer_ListsItsVirtualHosts_AndTheirQueuesWithCounts()
    {
        await DeclareAsync("it-list");
        await using var broker = brokers.OpenRabbitMq();
        await SendAsync(broker, "it-list", "a", "b");

        var server = await brokers.RabbitMqServer.GetSubscriptionsAsync(Ct).SingleAsync(Ct);
        var vhosts = await brokers.RabbitMqServer.GetNamespacesAsync(server.Id ?? "", Ct).ToListAsync(Ct);

        vhosts.Select(v => v.Name).ShouldContain("/");
        // The management API's counts lag behind by its statistics interval.
        await Eventually(async () => (await broker.Queues.GetQueuesAsync(Ct).SingleAsync(q => q.Name == "it-list", Ct)).ActiveMessageCount == 2);
    }

    [Test]
    public async Task Peek_ReturnsTheSentMessages_WithTheirProperties_AndPutsThemBack()
    {
        await DeclareAsync("it-peek");
        await using var broker = brokers.OpenRabbitMq();
        await broker.Messages.SendAsync("it-peek", [new OutgoingMessage
        {
            Body = "{\"id\":1}"u8.ToArray(), MessageId = "order-1", ContentType = "application/json", Subject = "order-created",
            CorrelationId = "c-1", Properties = [new("source", "it")],
        }], Ct).LastAsync(Ct);

        var first = await PeekAsync(broker, "it-peek");
        var again = await PeekAsync(broker, "it-peek");

        var message = first.ShouldHaveSingleItem();
        message.MessageId.ShouldBe("order-1");
        message.ContentType.ShouldBe("application/json");
        message.Subject.ShouldBe("order-created");
        message.BodyText.ShouldBe("{\"id\":1}");
        message.Properties.ShouldBe([new("source", "it")]);
        message.TransportProperties.Single(p => p.Id == "correlationId").Text.ShouldBe("c-1");
        message.TransportProperties.Single(p => p.Id == "redelivered").Text.ShouldBe("false");
        again.ShouldHaveSingleItem().TransportProperties.Single(p => p.Id == "redelivered").Text.ShouldBe("true");
        broker.ShouldBeAssignableTo<IHasPeek>().ShouldNotBeNull().Peek.Kind.ShouldBe(PeekKind.Requeues);
    }

    [Test]
    public async Task Received_Messages_AreHeld_CompleteRemovesOne_AbandonReturnsTheOther()
    {
        await DeclareAsync("it-settle");
        await using var broker = brokers.OpenRabbitMq();
        await SendAsync(broker, "it-settle", "complete me", "abandon me");

        await using (var locked = broker.Messages.StartReceiving(Queue("it-settle"), 2, Ct))
        {
            var held = await locked.Held.ReadAllAsync(Ct).ToListAsync(Ct);
            held.Select(m => m.BodyText).ShouldBe(["complete me", "abandon me"]);
            held.ShouldAllBe(m => m.State == MessageState.Locked && locked.IsHeld(m.Key));

            await locked.CompleteAsync(held[0], Ct);
            await locked.AbandonAsync(held[1], Ct);

            locked.IsHeld(held[0].Key).ShouldBeFalse();
            await Should.ThrowAsync<MessageLockLostException>(() => locked.CompleteAsync(held[0], Ct));
        }

        (await ReceiveAllAsync(broker, "it-settle")).Select(m => m.BodyText).ShouldBe(["abandon me"]);
    }

    [Test]
    public async Task Disposing_GivesBackWhatIsStillHeld_InOrder()
    {
        await DeclareAsync("it-give-back");
        await using var broker = brokers.OpenRabbitMq();
        await SendAsync(broker, "it-give-back", "one", "two", "three");

        await using (var locked = broker.Messages.StartReceiving(Queue("it-give-back"), 10, Ct))
            (await locked.Held.ReadAllAsync(Ct).CountAsync(Ct)).ShouldBe(3);

        (await ReceiveAllAsync(broker, "it-give-back")).Select(m => m.BodyText).ShouldBe(["one", "two", "three"]);
    }

    [Test]
    public async Task Rejecting_SendsItToTheDeadLetterQueue_WithTheReason()
    {
        await DeclareAsync("it-dead");
        await DeclareAsync("it-dead-letters", new() { ["x-dead-letter-exchange"] = "", ["x-dead-letter-routing-key"] = "it-dead" });
        await using var broker = brokers.OpenRabbitMq();
        await SendAsync(broker, "it-dead-letters", "poison");

        await using (var locked = broker.Messages.StartReceiving(Queue("it-dead-letters"), 1, Ct))
            await locked.DeadLetterAsync(await locked.Held.ReadAsync(Ct), "unused", Ct);

        var dead = (await PeekAsync(broker, "it-dead")).ShouldHaveSingleItem();
        dead.BodyText.ShouldBe("poison");
        dead.DeadLetterReason.ShouldBe("rejected");
        dead.DeadLetterErrorDescription.ShouldBe("Dead-lettered from queue it-dead-letters, 1 times");
    }

    [Test]
    public async Task Copies_KeepTheProperties_AndTheOriginalsStay()
    {
        await DeclareAsync("it-copy");
        await using var broker = brokers.OpenRabbitMq();
        await broker.Messages.SendAsync("it-copy", [new OutgoingMessage
        {
            Body = "original"u8.ToArray(), MessageId = "m-1", Subject = "kind", Properties = [new("source", "it")],
        }], Ct).LastAsync(Ct);
        var peeked = await PeekAsync(broker, "it-copy");

        (await broker.Messages.SendCopiesAsync(Queue("it-copy"), peeked, Ct).LastAsync(Ct)).ShouldBe(1);

        var both = await ReceiveAllAsync(broker, "it-copy");
        both.Select(m => (m.MessageId, m.Subject, m.BodyText)).ShouldBe([("m-1", "kind", "original"), ("m-1", "kind", "original")]);
        both[1].Properties.ShouldBe([new("source", "it")]);
    }

    [Test]
    public async Task Purge_RemovesEverything_AndCountsIt()
    {
        await DeclareAsync("it-purge");
        await using var broker = brokers.OpenRabbitMq();
        await SendAsync(broker, "it-purge", "a", "b", "c");

        (await broker.Messages.PurgeAsync(Queue("it-purge"), Ct).LastAsync(Ct)).ShouldBe(3);
        (await ReceiveAllAsync(broker, "it-purge")).ShouldBeEmpty();
    }

    [Test]
    public async Task Sending_ToAMissingQueue_Fails()
    {
        await using var broker = brokers.OpenRabbitMq();

        var failure = await Should.ThrowAsync<InvalidOperationException>(() => SendAsync(broker, "it-missing", "lost"));

        failure.Message.ShouldBe("No queue it-missing in virtual host /");
    }

    private static MessageSource.Queue Queue(string name) => new(name);

    private static Task SendAsync(IBroker broker, string queue, params string[] bodies) =>
        broker.Messages.SendAsync(queue, [.. bodies.Select(b => new OutgoingMessage { Body = System.Text.Encoding.UTF8.GetBytes(b) })], Ct).LastAsync(Ct).AsTask();

    private static async Task<IReadOnlyList<MessageInfo>> PeekAsync(IBroker broker, string queue) =>
        [.. (await broker.ShouldBeAssignableTo<IHasPeek>().ShouldNotBeNull().Peek.PeekAsync(Queue(queue), 100, Ct).ToListAsync(Ct)).SelectMany(p => p)];

    private static async Task<IReadOnlyList<MessageInfo>> ReceiveAllAsync(IBroker broker, string queue) =>
        [.. (await broker.Messages.ReceiveAndDeleteAsync(Queue(queue), 100, Ct).ToListAsync(Ct)).SelectMany(p => p)];

    private async Task DeclareAsync(string queue, Dictionary<string, object?>? arguments = null)
    {
        var options = brokers.RabbitMq;
        await using var connection = await new ConnectionFactory { Uri = options.Amqp, UserName = options.User, Password = options.Password }
            .CreateConnectionAsync(Ct);
        await using var channel = await connection.CreateChannelAsync(cancellationToken: Ct);
        await channel.QueueDeclareAsync(queue, durable: true, exclusive: false, autoDelete: false,
            arguments: new Dictionary<string, object?>(arguments ?? []) { ["x-queue-type"] = "quorum" }, cancellationToken: Ct);
    }

    private static async Task Eventually(Func<Task<bool>> condition)
    {
        var until = DateTime.UtcNow.AddSeconds(30);
        while (!await condition())
        {
            if (DateTime.UtcNow > until) throw new TimeoutException("The condition did not hold within 30 s.");
            await Task.Delay(500, Ct);
        }
    }
}
