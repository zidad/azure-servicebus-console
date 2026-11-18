using System.Text;

namespace ServiceBusConsole.IntegrationTests;

/// <summary>The Azure transport against the real emulator: what the fakes in the UI tests stand in for.</summary>
[ClassDataSource<Emulator>(Shared = SharedType.PerTestSession)]
public sealed class EmulatorTests(Emulator emulator)
{
    [Before(Test)]
    public void RequireEmulator() =>
        Skip.Unless(Emulator.Enabled, $"Needs Docker: set {Emulator.EnableVariable}=1 to run the emulator tests.");

    [Test]
    public async Task Queues_ComeFromTheConfigFile_WithPeekedCounts()
    {
        await using var ns = emulator.Open();
        await SendAsync(ns, "it-list", "a", "b");

        var queues = await ns.Queues.GetQueuesAsync(Ct).ToListAsync(Ct);

        queues.Select(q => q.Name).ShouldBe(["it-list", "it-peek", "it-settle", "it-retry"], ignoreOrder: true);
        queues.Single(q => q.Name == "it-list").ActiveMessageCount.ShouldBe(2);
        ns.ShouldBeAssignableTo<IHasTopics>().ShouldNotBeNull();
        ns.ShouldNotBeAssignableTo<IHasMetrics>();
    }

    [Test]
    public async Task Peek_ReturnsTheSentMessages_WithTheirProperties_AndLeavesThem()
    {
        await using var ns = emulator.Open();
        await ns.Messages.SendAsync("it-peek", [new OutgoingMessage
        {
            Body = "{\"id\":1}"u8.ToArray(), MessageId = "order-1", ContentType = "application/json", Subject = "order-created",
            Properties = [new("source", "it")],
        }], Ct).LastAsync(Ct);

        var first = await PeekAsync(ns, "it-peek");
        var again = await PeekAsync(ns, "it-peek");

        var message = first.ShouldHaveSingleItem();
        message.MessageId.ShouldBe("order-1");
        message.ContentType.ShouldBe("application/json");
        message.Subject.ShouldBe("order-created");
        message.BodyText.ShouldBe("{\"id\":1}");
        message.Properties.ShouldContain(new KeyValuePair<string, string>("source", "it"));
        message.Key.ShouldBe(MessageKey.From(message.SequenceNumber));
        again.ShouldHaveSingleItem().Key.ShouldBe(message.Key);
    }

    [Test]
    public async Task Received_Messages_AreHeld_CompleteRemovesOne_AbandonReturnsTheOther()
    {
        await using var ns = emulator.Open();
        await SendAsync(ns, "it-settle", "complete me", "abandon me");
        var source = new MessageSource.Queue("it-settle");

        await using (var locked = ns.Messages.StartReceiving(source, 2, Ct))
        {
            var held = await locked.Held.ReadAllAsync(Ct).ToListAsync(Ct);
            held.Select(m => m.BodyText).ShouldBe(["complete me", "abandon me"]);
            held.ShouldAllBe(m => m.State == MessageState.Locked && locked.IsHeld(m.Key));

            await locked.CompleteAsync(held[0], Ct);
            await locked.AbandonAsync(held[1], Ct);
            locked.IsHeld(held[0].Key).ShouldBeFalse();
            locked.IsHeld(held[1].Key).ShouldBeFalse();
        }

        var left = (await PeekAsync(ns, "it-settle")).ShouldHaveSingleItem();
        left.BodyText.ShouldBe("abandon me");
        left.DeliveryCount.ShouldBe(1);
    }

    [Test]
    public async Task DeadLettered_Messages_RetryLikeTheBrowser_LockThenCopyThenComplete()
    {
        await using var ns = emulator.Open();
        await SendAsync(ns, "it-retry", "one", "two", "three");
        await using (var locked = ns.Messages.StartReceiving(new MessageSource.Queue("it-retry"), 3, Ct))
            await foreach (var m in locked.Held.ReadAllAsync(Ct))
                await locked.DeadLetterAsync(m, "it: poison", Ct);
        var dlq = new MessageSource.Queue("it-retry", IsDlq: true);
        var dead = await PeekAsync(ns, dlq);
        dead.ShouldAllBe(m => m.DeadLetterReason == "it: poison");
        var wanted = dead.Where(m => m.BodyText != "two").ToList();

        await using (var locked = ns.Messages.StartLocking(dlq, [.. wanted.Select(m => m.Key)], Ct))
        {
            var held = await locked.Held.ReadAllAsync(Ct).ToListAsync(Ct);
            held.Select(m => m.Key).ShouldBe(wanted.Select(m => m.Key), ignoreOrder: true);
            (await ns.Messages.SendCopiesAsync(dlq, held, Ct).LastAsync(Ct)).ShouldBe(2);
            foreach (var m in held) await locked.CompleteAsync(m, Ct);
        }

        (await PeekAsync(ns, "it-retry")).Select(m => m.BodyText).ShouldBe(["one", "three"]);
        (await PeekAsync(ns, dlq)).ShouldHaveSingleItem().BodyText.ShouldBe("two");
    }

    [Test]
    public async Task Topic_Messages_ReachItsSubscription()
    {
        await using var ns = emulator.Open();
        await SendAsync(ns, "it-events", "event");
        var topics = ns.ShouldBeAssignableTo<IHasTopics>().ShouldNotBeNull().Topics;

        // The emulator forwards to subscriptions in the background.
        var messages = await UntilAsync(() => PeekAsync(ns, new MessageSource.Subscription("it-events", "it-audit")), m => m.Count > 0);
        var subscriptions = await topics.GetSubscriptionsAsync("it-events", Ct).ToListAsync(Ct);

        messages.ShouldHaveSingleItem().BodyText.ShouldBe("event");
        subscriptions.ShouldHaveSingleItem().Name.ShouldBe("it-audit");
        subscriptions[0].ActiveMessageCount.ShouldBe(1);
    }

    private static async Task<T> UntilAsync<T>(Func<Task<T>> read, Func<T, bool> done)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(Ct);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        while (true)
        {
            var value = await read();
            if (done(value)) return value;
            await Task.Delay(200, timeout.Token);
        }
    }

    private static async Task SendAsync(IBroker ns, string queueOrTopic, params string[] bodies) =>
        await ns.Messages.SendAsync(queueOrTopic, [.. bodies.Select(b => new OutgoingMessage { Body = Encoding.UTF8.GetBytes(b) })], Ct)
            .LastAsync(Ct);

    private static Task<List<MessageInfo>> PeekAsync(IBroker ns, string queue) => PeekAsync(ns, new MessageSource.Queue(queue));

    private static async Task<List<MessageInfo>> PeekAsync(IBroker ns, MessageSource source) =>
        await ns.Messages.PeekAsync(source, 100, ct: Ct).SelectMany(page => page.ToAsyncEnumerable()).ToListAsync(Ct);
}
