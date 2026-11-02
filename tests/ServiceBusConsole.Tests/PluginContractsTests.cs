using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Tests;

public sealed class PluginContractsTests
{
    private static readonly FakePluginHost Host = new();

    private static MessageActionContext Context(MessageSource source, string? deadLetterReason = null, params (string Key, string? Value)[] settings) => new()
    {
        PluginId = "fake",
        Message = new MessageInfo
        {
            SequenceNumber = 42,
            State = MessageState.Peeked,
            MessageId = "msg-42",
            Subject = "order-created",
            DeadLetterReason = deadLetterReason,
        },
        Source = source,
        Session = new SessionSnapshot { At = DateTimeOffset.UnixEpoch, Namespace = AppHarness.AzureNamespace },
        Settings = new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value))).Build(),
        Host = Host,
        Progress = CallbackProgress.None,
    };

    [Test]
    public void Template_FillsTheMessagesPlaceholders_IgnoringCase_AndKeepsUnknownOnes()
    {
        var context = Context(new MessageSource.Queue("orders", IsDlq: true), "MaxDeliveryCountExceeded");

        Template.Fill("[{entity}] {subject} #{sequenceNumber} ({messageId}) in {namespace}: {deadLetterReason} {unknown}", context)
            .ShouldBe("[orders DLQ] order-created #42 (msg-42) in sb-orders-dev: MaxDeliveryCountExceeded {unknown}");
        Template.Fill("{SUBJECT}/{DeadLetterReason}", Context(new MessageSource.Queue("orders"))).ShouldBe("order-created/");
    }

    [Test]
    public void MessageFileName_IsTheEntityAndSequenceNumber_SafeForFiles()
    {
        PluginFiles.MessageFileName(Context(new MessageSource.Queue("orders", IsDlq: true)), ".json").ShouldBe("orders-dlq-42.json");
        PluginFiles.MessageFileName(Context(new MessageSource.Subscription("Billing.Events", "audit/v2")), "md").ShouldBe("billing-events-audit-v2-42.md");
        PluginFiles.MessageFileName(Context(new MessageSource.Queue("../etc")), "md").ShouldBe("etc-42.md");
    }

    [Test]
    public void Files_GoUnderTheStateDirectory()
    {
        var context = Context(new MessageSource.Queue("orders"));

        PluginFiles.MessagePath(context, "json").ShouldBe(Path.Combine(Host.StateDirectory, "messages", "orders-42.json"));
        PluginFiles.PluginDirectory(context).ShouldBe(Path.Combine(Host.StateDirectory, "plugins", "fake"));
        Directory.Exists(Path.Combine(Host.StateDirectory, "messages")).ShouldBeTrue();
        Directory.Delete(Host.StateDirectory, recursive: true);
    }

    [Test]
    public void RequiredSetting_SaysHowToSetIt()
    {
        Context(new MessageSource.Queue("orders"), null, ("repo", "acme/orders")).RequiredSetting("repo").ShouldBe("acme/orders");

        Should.Throw<InvalidOperationException>(() => Context(new MessageSource.Queue("orders")).RequiredSetting("repo"))
            .Message.ShouldBe("Set plugins.fake.repo first: azsb config set plugins.fake.repo <value>");
    }

    [Test]
    public void EnsureSuccess_ThrowsWithTheFirstLineOfErrorOutput()
    {
        var ok = new ProcessResult(0, "out", "");
        ok.EnsureSuccess("gh").ShouldBeSameAs(ok);

        Should.Throw<ProcessFailedException>(() => new ProcessResult(1, "usage", "\n  gh: not logged in\nrun gh auth login\n").EnsureSuccess("gh"))
            .Message.ShouldBe("gh failed (exit code 1): gh: not logged in");
        Should.Throw<ProcessFailedException>(() => new ProcessResult(2, "", "").EnsureSuccess("code")).Message.ShouldBe("code failed (exit code 2): no output");
    }

    [Test]
    public async Task ProcessHost_RunsWithArgumentsAsGiven_AndCollectsOutput()
    {
        var host = new ProcessPluginHost(Path.GetTempPath());

        var result = await host.RunProcessAsync(new ProcessSpec("dotnet", ["--version"]), Ct);

        result.ExitCode.ShouldBe(0);
        result.StandardOutput.Trim().ShouldNotBeEmpty();
        await Should.ThrowAsync<InvalidOperationException>(() => host.RunProcessAsync(new ProcessSpec("azsb-no-such-program", []), Ct));
    }
}
