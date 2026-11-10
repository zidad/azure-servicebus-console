using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.Tests;

public sealed class NamespaceCommandTests
{
    private sealed class Cli
    {
        public FakeServices Services { get; } = new();
        public FakeNamespaceService Namespaces { get; } = new();
        public Dictionary<string, string?> Settings { get; } = [];
        public StringWriter Out { get; private set; } = new();
        public StringWriter Error { get; private set; } = new();
        public string? Input { get; set; }

        public Cli()
        {
            Namespaces.Subscriptions.Add(AppHarness.AzureSubscription);
            Namespaces.Namespaces.Add(AppHarness.AzureNamespace);
            Namespaces.CurrentSubscriptionId = AppHarness.AzureSubscription.Id;
        }

        public AppCommands Commands => new(Host.CreateDefaultBuilder(), PluginCatalog.Empty, () =>
            new CommandServices(
                Namespaces,
                new FakeNamespaceOpener(info => FakeNamespace.For(info, Services)),
                new ConfigurationBuilder().AddInMemoryCollection(Settings).Build())
            {
                Out = Out, Error = Error, Input = () => Input is { } text ? new MemoryStream(Encoding.UTF8.GetBytes(text)) : null,
            });

        public async Task<int> RunAsync(params string[] args)
        {
            Out = new();
            Error = new();
            return await Commands.Root.Parse(args).InvokeAsync(cancellationToken: Ct);
        }

        public JsonNode Json => JsonNode.Parse(Out.ToString()).ShouldNotBeNull();
    }

    private static MessageInfo Message(long sequence, string id = "", string body = "{}", params KeyValuePair<string, string>[] properties) => new()
    {
        SequenceNumber = sequence, State = MessageState.Peeked, MessageId = id.Length > 0 ? id : $"msg-{sequence}",
        Body = Encoding.UTF8.GetBytes(body), Properties = properties, DeadLetterReason = "MaxDeliveryCountExceeded",
    };

    [Test]
    public void Peek_BindsItsOptions()
    {
        var input = new Cli().Commands.Bind("peek", "orders", "--dlq", "--max", "5", "-s", "Contoso Development", "-n", "sb-*", "--json")
            .ShouldBeOfType<NamespaceInput>();

        input.Command.ShouldBe(new PeekCommand("orders", Dlq: true, Max: 5));
        input.Target.ShouldBe(new NamespaceTarget("Contoso Development", "sb-*"));
        input.Json.ShouldBeTrue();
    }

    [Test]
    public void Retry_BindsRepeatedIds()
    {
        var command = new Cli().Commands.Bind("retry", "orders", "--message-id", "a", "--message-id", "b", "--sequence", "7")
            .ShouldBeOfType<NamespaceInput>().Command.ShouldBeOfType<RetryCommand>();

        command.Selection.MessageIds.ShouldBe(["a", "b"]);
        command.Selection.SequenceNumbers.ShouldBe([7L]);
    }

    [Test]
    public async Task NoFlags_UseTheCurrentSubscription_AndItsOnlyNamespace()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders", ActiveMessageCount = 4, DeadLetterMessageCount = 2 });

        (await cli.RunAsync("list", "--json")).ShouldBe(ExitCodes.Ok);

        cli.Json[0]!["path"]!.GetValue<string>().ShouldBe("orders");
        cli.Json[0]!["deadLetter"]!.GetValue<long>().ShouldBe(2);
    }

    [Test]
    public async Task NamespacePattern_MatchingOne_IsUsed()
    {
        var cli = new Cli();
        cli.Namespaces.Namespaces.Add(AppHarness.AzureNamespace with { Name = "sb-billing-tst", FullyQualifiedNamespace = "sb-billing-tst.servicebus.windows.net" });

        (await cli.RunAsync("list", "-n", "sb-orders-*")).ShouldBe(ExitCodes.Ok);
        (await cli.RunAsync("list", "-n", "sb-*")).ShouldBe(ExitCodes.Usage);
        cli.Error.ToString().ShouldContain("sb-billing-tst, sb-orders-dev");
        (await cli.RunAsync("list")).ShouldBe(ExitCodes.Usage);
        (await cli.RunAsync("list", "-n", "sb-nothing-*")).ShouldBe(ExitCodes.NotFound);
    }

    [Test]
    public async Task UnknownSubscription_IsNotFound()
    {
        var cli = new Cli();

        (await cli.RunAsync("list", "-s", "Fabrikam")).ShouldBe(ExitCodes.NotFound);
        cli.Error.ToString().ShouldContain("Fabrikam");
    }

    [Test]
    public async Task List_DeadLetters_ShowsQueuesAndSubscriptions_WithForwarding()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders", DeadLetterMessageCount = 1 });
        cli.Services.Queues.Add(new QueueInfo { Name = "empty" });
        cli.Services.Topics.Topics.Add(new TopicInfo { Name = "events" });
        cli.Services.Topics.Subscriptions.Add(new SubscriptionInfo { Name = "billing", TopicName = "events", DeadLetterMessageCount = 3, AccessedAt = DateTimeOffset.Parse("2026-10-01T08:00:00Z") });
        cli.Services.Topics.Subscriptions.Add(new SubscriptionInfo { Name = "audit", TopicName = "events", DeadLetterMessageCount = 1 });
        cli.Services.Topics.Forwarding.Add(new("events", "billing", "billing-queue", null));
        cli.Services.Topics.Forwarding.Add(new("events", "audit", "sb://sb-orders-dev.servicebus.windows.net/orders", null));

        (await cli.RunAsync("list", "--dead-letters", "--json")).ShouldBe(ExitCodes.Ok);

        var rows = cli.Json.AsArray();
        rows.Select(r => r!["path"]!.GetValue<string>()).ShouldBe(["orders", "events/audit", "events/billing"]);
        rows[2]!["forwardTo"]!.GetValue<string>().ShouldBe("billing-queue");
        rows[2]!["forwardTargetExists"]!.GetValue<bool>().ShouldBeFalse();
        rows[1]!["forwardTo"]!.GetValue<string>().ShouldBe("orders");
        rows[1]!["forwardTargetExists"]!.GetValue<bool>().ShouldBeTrue();

        (await cli.RunAsync("list", "--dead-letters")).ShouldBe(ExitCodes.Ok);
        cli.Out.ToString().ShouldContain("billing-queue (missing)");
    }

    [Test]
    public async Task Peek_PrintsMessages()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(5, "abc", """{"orderId":"A-1"}""", KeyValuePair.Create("MT-Fault-Message", "boom")));

        (await cli.RunAsync("peek", "orders", "--dlq")).ShouldBe(ExitCodes.Ok);

        var text = cli.Out.ToString();
        text.ShouldContain("#5  abc");
        text.ShouldContain("MaxDeliveryCountExceeded");
        text.ShouldContain("MT-Fault-Message  boom");
        text.ShouldContain("\"orderId\": \"A-1\"");
        cli.Services.Messages.Peeked.ShouldBe([new MessageSource.Queue("orders", IsDlq: true)]);
    }

    [Test]
    public async Task Peek_TakesAzurePaths()
    {
        var cli = new Cli();
        cli.Services.Topics.Topics.Add(new TopicInfo { Name = "events" });
        cli.Services.Topics.Subscriptions.Add(new SubscriptionInfo { Name = "billing", TopicName = "events" });

        (await cli.RunAsync("peek", "events/Subscriptions/billing/$DeadLetterQueue", "--json")).ShouldBe(ExitCodes.Ok);

        cli.Services.Messages.Peeked.ShouldBe([new MessageSource.Subscription("events", "billing", IsDlq: true)]);
    }

    [Test]
    public async Task Peek_TopicWithSlashes()
    {
        var cli = new Cli();
        cli.Services.Topics.Topics.Add(new TopicInfo { Name = "patients.commands" });
        cli.Services.Topics.Topics.Add(new TopicInfo { Name = "patients.commands/createflag" });
        cli.Services.Topics.Subscriptions.Add(new SubscriptionInfo { Name = "IHasFlag-Events", TopicName = "patients.commands/createflag" });

        (await cli.RunAsync("peek", "patients.commands/createflag/IHasFlag-Events", "--dlq")).ShouldBe(ExitCodes.Ok);

        cli.Services.Messages.Peeked.ShouldBe([new MessageSource.Subscription("patients.commands/createflag", "IHasFlag-Events", IsDlq: true)]);
    }

    [Test]
    public async Task Peek_UnknownEntity_IsNotFound()
    {
        var cli = new Cli();

        (await cli.RunAsync("peek", "nope")).ShouldBe(ExitCodes.NotFound);
        cli.Error.ToString().ShouldContain("nope");
    }

    [Test]
    public async Task Peek_LongBody_IsCut_UnlessFull()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1, body: new string('x', 5000)));

        await cli.RunAsync("peek", "orders");
        cli.Out.ToString().ShouldContain("--full");
        await cli.RunAsync("peek", "orders", "--full");
        cli.Out.ToString().ShouldNotContain("--full");
    }

    [Test]
    public async Task Peek_RedactsConfiguredFields_InMatchingNamespaces()
    {
        var cli = new Cli();
        cli.Settings["cli:redact:namespaces"] = "sb-orders-*";
        cli.Settings["cli:redact:fields"] = "name, identifier.value, title:scalar, value*, X-Patient";
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1, body: """
            {"message":{"resourceType":"Patient","id":"p-1","name":[{"family":"Jansen"}],
             "identifier":[{"system":"bsn","value":"123456782"}],"title":{"text":"kept"},"valueQuantity":{"value":80}}}
            """, properties: [new("X-Patient", "Jansen"), new("MessageType", "PatientImported")]));
        cli.Services.Messages.Messages.Add(Message(2, body: "Jansen, plain text"));

        (await cli.RunAsync("peek", "orders", "--json")).ShouldBe(ExitCodes.Ok);

        var first = cli.Json[0]!;
        var patient = first["body"]!["message"]!;
        patient["name"]!.GetValue<string>().ShouldBe(Redaction.Redacted);
        patient["identifier"]![0]!["value"]!.GetValue<string>().ShouldBe(Redaction.Redacted);
        patient["identifier"]![0]!["system"]!.GetValue<string>().ShouldBe("bsn");
        patient["title"]!["text"]!.GetValue<string>().ShouldBe("kept");
        patient["valueQuantity"]!.GetValue<string>().ShouldBe(Redaction.Redacted);
        patient["id"]!.GetValue<string>().ShouldBe("p-1");
        first["properties"]!["X-Patient"]!.GetValue<string>().ShouldBe(Redaction.Redacted);
        first["properties"]!["MessageType"]!.GetValue<string>().ShouldBe("PatientImported");
        cli.Json[1]!["body"]!.GetValue<string>().ShouldNotContain("Jansen");
        cli.Error.ToString().ShouldContain("redacted");
    }

    [Test]
    public async Task Peek_OtherNamespaces_AreNotRedacted()
    {
        var cli = new Cli();
        cli.Settings["cli:redact:namespaces"] = "sb-*-prd";
        cli.Settings["cli:redact:fields"] = "name";
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1, body: """{"name":"Jansen"}"""));

        await cli.RunAsync("peek", "orders", "--json");

        cli.Json[0]!["body"]!["name"]!.GetValue<string>().ShouldBe("Jansen");
    }

    [Test]
    public async Task Retry_ById_MovesEveryMatch_InTransactions()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.AddRange([Message(1, "a"), Message(2, "b"), Message(3, "a")]);

        (await cli.RunAsync("retry", "orders", "--message-id", "a", "--json")).ShouldBe(ExitCodes.Ok);

        cli.Services.Transactions.Moved.SelectMany(c => c).ShouldBe([1L, 3L]);
        cli.Json["done"]!.AsArray().Select(m => m!["sequenceNumber"]!.GetValue<long>()).ShouldBe([1L, 3L]);
        cli.Services.Messages.Peeked.ShouldAllBe(s => s.IsDlq);
    }

    [Test]
    public async Task Retry_MissingId_IsNotFound_AndTheRestIsDone()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1, "a"));

        (await cli.RunAsync("retry", "orders", "--message-id", "a", "--message-id", "gone")).ShouldBe(ExitCodes.NotFound);

        cli.Services.Transactions.Moved.SelectMany(c => c).ShouldBe([1L]);
        cli.Out.ToString().ShouldContain("gone");
    }

    [Test]
    public async Task Retry_WithoutSelection_IsUsage()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });

        (await cli.RunAsync("retry", "orders")).ShouldBe(ExitCodes.Usage);
    }

    [Test]
    public async Task Retry_DryRun_ChangesNothing()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.AddRange([Message(1), Message(2)]);

        (await cli.RunAsync("retry", "orders", "--all", "--dry-run")).ShouldBe(ExitCodes.Ok);

        cli.Services.Transactions.Moved.ShouldBeEmpty();
        cli.Out.ToString().ShouldContain("Dry run");
    }

    [Test]
    public async Task Retry_All_StopsAtTheLimit()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.AddRange(Enumerable.Range(1, 10).Select(i => Message(i)));

        (await cli.RunAsync("retry", "orders", "--all", "--limit", "4")).ShouldBe(ExitCodes.Ok);

        cli.Services.Transactions.Moved.SelectMany(c => c).ShouldBe([1L, 2L, 3L, 4L]);
    }

    [Test]
    public async Task ProtectedNamespace_NeedsConfirm_ForChanges()
    {
        var cli = new Cli();
        cli.Settings["cli:protect"] = "sb-orders-*";
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1, "a"));

        (await cli.RunAsync("retry", "orders", "--message-id", "a")).ShouldBe(ExitCodes.Usage);
        cli.Error.ToString().ShouldContain("--confirm sb-orders-dev");
        cli.Services.Transactions.Moved.ShouldBeEmpty();

        (await cli.RunAsync("retry", "orders", "--message-id", "a", "--dry-run")).ShouldBe(ExitCodes.Ok);
        (await cli.RunAsync("peek", "orders")).ShouldBe(ExitCodes.Ok);
        (await cli.RunAsync("retry", "orders", "--message-id", "a", "--confirm", "sb-orders-dev")).ShouldBe(ExitCodes.Ok);
        cli.Services.Transactions.Moved.SelectMany(c => c).ShouldBe([1L]);
    }

    [Test]
    public async Task Delete_NeedsDlq()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.Add(Message(1));

        (await cli.RunAsync("delete", "orders", "--sequence", "1")).ShouldBe(ExitCodes.Usage);
        cli.Services.Messages.Completed.ShouldBeEmpty();
    }

    [Test]
    public async Task Delete_BySequence_CompletesThem()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.AddRange([Message(1), Message(2), Message(3)]);

        (await cli.RunAsync("delete", "orders", "--dlq", "--sequence", "1", "--sequence", "3")).ShouldBe(ExitCodes.Ok);

        cli.Services.Messages.Completed.Order().ShouldBe([1L, 3L]);
        cli.Out.ToString().ShouldContain("Deleted 2 of 2");
    }

    [Test]
    public async Task Delete_MessageLockedElsewhere_Fails()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });
        cli.Services.Messages.Messages.AddRange([Message(1), Message(2)]);
        cli.Services.Messages.LockedElsewhere.Add(2);

        (await cli.RunAsync("delete", "orders", "--dlq", "--all", "--json")).ShouldBe(ExitCodes.Failed);

        cli.Json["failed"]![0]!["sequenceNumber"]!.GetValue<long>().ShouldBe(2);
    }

    [Test]
    public async Task Purge_NeedsYes()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });

        (await cli.RunAsync("purge", "orders", "--dlq")).ShouldBe(ExitCodes.Usage);
        cli.Services.Messages.Purged.ShouldBeEmpty();
        (await cli.RunAsync("purge", "orders", "--dlq", "--yes")).ShouldBe(ExitCodes.Ok);
        cli.Services.Messages.Purged.ShouldBe([new MessageSource.Queue("orders", IsDlq: true)]);
    }

    [Test]
    public async Task Send_SendsTheBodyAndProperties()
    {
        var cli = new Cli();
        cli.Services.Topics.Topics.Add(new TopicInfo { Name = "events" });

        (await cli.RunAsync("send", "events", "--body", """{"id":1}""", "--content-type", "application/json", "--property", "source=cli", "--property", "a=b=c"))
            .ShouldBe(ExitCodes.Ok);

        var (target, message) = cli.Services.Messages.SentNew.ShouldHaveSingleItem();
        target.ShouldBe("events");
        Encoding.UTF8.GetString(message.Body.Span).ShouldBe("""{"id":1}""");
        message.ContentType.ShouldBe("application/json");
        message.Properties.ShouldBe([new("source", "cli"), new("a", "b=c")]);
    }

    [Test]
    public async Task Send_ReadsStandardInput()
    {
        var cli = new Cli { Input = "piped" };
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });

        (await cli.RunAsync("send", "orders")).ShouldBe(ExitCodes.Ok);

        Encoding.UTF8.GetString(cli.Services.Messages.SentNew.ShouldHaveSingleItem().Message.Body.Span).ShouldBe("piped");
    }

    [Test]
    public async Task Send_WithoutBody_IsUsage()
    {
        var cli = new Cli();
        cli.Services.Queues.Add(new QueueInfo { Name = "orders" });

        (await cli.RunAsync("send", "orders")).ShouldBe(ExitCodes.Usage);
    }

    [Test]
    public async Task Check_ReportsEachStep_UntilOneFails()
    {
        var cli = new Cli();

        await cli.RunAsync("check", "-s", "emulator", "--json");

        var checks = cli.Json["checks"]!.AsArray();
        checks[0]!["ok"]!.GetValue<bool>().ShouldBeTrue();
        checks[1]!["name"]!.GetValue<string>().ShouldBe("DNS");
        checks[1]!["detail"]!.GetValue<string>().ShouldContain("private");
    }

    [Test]
    public async Task Check_UnknownNamespace_FailsAtTheFirstStep()
    {
        var cli = new Cli();

        (await cli.RunAsync("check", "-n", "nope")).ShouldBe(ExitCodes.Failed);

        cli.Out.ToString().ShouldStartWith("✗ Namespace");
    }

    [Test]
    [Arguments("10.1.2.3", true)]
    [Arguments("172.20.0.1", true)]
    [Arguments("192.168.1.1", true)]
    [Arguments("127.0.0.1", true)]
    [Arguments("20.50.2.1", false)]
    [Arguments("172.32.0.1", false)]
    public void PrivateAddresses(string address, bool isPrivate) => CheckCommand.IsPrivate(IPAddress.Parse(address)).ShouldBe(isPrivate);

    [Test]
    public void Glob_MatchesWholeNames_IgnoringCase()
    {
        new Glob("sbns-pro-prd-we-*").Matches("SBNS-pro-prd-we-001-x7").ShouldBeTrue();
        new Glob("sb-?").Matches("sb-1").ShouldBeTrue();
        new Glob("sb-*").Matches("xsb-1").ShouldBeFalse();
        new Glob("sb.orders").Matches("sbxorders").ShouldBeFalse();
    }
}
