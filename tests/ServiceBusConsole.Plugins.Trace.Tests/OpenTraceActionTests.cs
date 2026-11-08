using System.IO.Compression;
using System.Text;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins.Trace.Tests;

public sealed class OpenTraceActionTests
{
    private const string TraceId = "4bf92f3577b34da6a3ce929d0e0e4736";
    private const string AppInsightsId = "/subscriptions/sub-1/resourceGroups/rg-obs/providers/microsoft.insights/components/ai-orders";

    private static readonly MessageInfo Message = new()
    {
        SequenceNumber = 42,
        State = MessageState.Peeked,
        MessageId = "msg-42",
        Subject = "order-created",
        EnqueuedTime = new DateTimeOffset(2026, 11, 3, 10, 30, 0, TimeSpan.FromHours(1)),
        Properties = [new("tenant", "acme"), new("Diagnostic-Id", $"00-{TraceId}-00f067aa0ba902b7-01")],
        Body = Encoding.UTF8.GetBytes("{}"),
    };

    private static MessageActionContext Context(
        FakePluginHost host, MessageInfo? message = null, AzureSubscription? subscription = null, params (string Key, string Value)[] settings) => new()
    {
        PluginId = "trace",
        Message = message ?? Message,
        Source = new MessageSource.Queue("orders", IsDlq: true),
        Session = new SessionSnapshot { At = DateTimeOffset.UnixEpoch, Subscription = subscription ?? new AzureSubscription("Orders", "sub-1") },
        Settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new TracePlugin().Settings.Where(s => s.Default is not null).Select(s => KeyValuePair.Create(s.Key, s.Default)))
            .AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value)))
            .Build(),
        Host = host,
        Progress = new Progress<string>(),
    };

    private static Dictionary<string, string> PortalSegments(string url)
    {
        var parts = url[(url.IndexOf("/LogsBlade/", StringComparison.Ordinal) + "/LogsBlade/".Length)..].Split('/');
        return Enumerable.Range(0, parts.Length / 2).ToDictionary(i => parts[2 * i], i => Uri.UnescapeDataString(parts[2 * i + 1]));
    }

    private static string Gunzip(string base64)
    {
        using var gzip = new GZipStream(new MemoryStream(Convert.FromBase64String(base64)), CompressionMode.Decompress);
        using var reader = new StreamReader(gzip);
        return reader.ReadToEnd();
    }

    [Test]
    [Arguments("00-4bf92f3577b34da6a3ce929d0e0e4736-00f067aa0ba902b7-01", "4bf92f3577b34da6a3ce929d0e0e4736")]
    [Arguments(" 00-4BF92F3577B34DA6A3CE929D0E0E4736-00F067AA0BA902B7-00 ", "4bf92f3577b34da6a3ce929d0e0e4736")]
    [Arguments("00-00000000000000000000000000000000-00f067aa0ba902b7-01", null)]
    [Arguments("|4bf92f3577b34da6.1.", null)]
    [Arguments("", null)]
    public void Parse_ReadsW3CTraceParents(string value, string? traceId) =>
        TraceContext.Parse(value)?.TraceId.ShouldBe(traceId);

    [Test]
    public void Of_PrefersTraceparent_AndIgnoresTheCaseOfNames()
    {
        var message = Message with
        {
            Properties = [new("diagnostic-id", $"00-{TraceId}-00f067aa0ba902b7-01"), new("TraceParent", "00-11111111111111111111111111111111-2222222222222222-01")],
        };

        TraceContext.Of(message).ShouldBe(new TraceContext("11111111111111111111111111111111", "2222222222222222"));
        TraceContext.Of(Message with { Properties = [] }).ShouldBeNull();
    }

    [Test]
    public void CanRun_OnlyForMessagesWithATraceContext()
    {
        using var host = new FakePluginHost();

        new OpenTraceAction().CanRun(Context(host)).ShouldBeTrue();
        new OpenTraceAction().CanRun(Context(host, Message with { Properties = [new("tenant", "acme")] })).ShouldBeFalse();
    }

    [Test]
    public async Task Run_OpensTheTraceQueryInTheConfiguredApplicationInsights()
    {
        using var host = new FakePluginHost();

        var result = await new OpenTraceAction().RunAsync(Context(host, settings: [("appInsights", AppInsightsId), ("tenant", "contoso.com")]), Ct);

        var url = host.Opened.ShouldHaveSingleItem();
        url.ShouldStartWith("https://portal.azure.com/#@contoso.com/blade/Microsoft_Azure_Monitoring_Logs/LogsBlade/resourceId/%2Fsubscriptions%2Fsub-1%2F");
        var segments = PortalSegments(url);
        segments["resourceId"].ShouldBe(AppInsightsId);
        segments["source"].ShouldBe("LogsBlade.AnalyticsShareLinkToQuery");
        Gunzip(segments["q"]).ShouldContain($"where operation_Id == \"{TraceId}\"");
        segments["timespan"].ShouldBe("2026-11-03T08:30:00.000Z/2026-11-04T09:30:00.000Z");
        result.Text.ShouldBe($"Opened trace {TraceId} in Application Insights ai-orders");
        result.Link.ShouldNotBeNull().AbsoluteUri.ShouldBe(url);
        host.Processes.ShouldBeEmpty();
    }

    [Test]
    public async Task Run_WithoutTenant_UsesTheDefaultDirectory()
    {
        using var host = new FakePluginHost();

        await new OpenTraceAction().RunAsync(Context(host, settings: ("appInsights", AppInsightsId)), Ct);

        host.Opened.ShouldHaveSingleItem().ShouldStartWith("https://portal.azure.com/#blade/Microsoft_Azure_Monitoring_Logs/LogsBlade/");
    }

    [Test]
    public async Task Run_FillsTheUrlTemplate()
    {
        using var host = new FakePluginHost();

        var result = await new OpenTraceAction().RunAsync(
            Context(host, settings: ("url", "http://localhost:16686/trace/{traceId}?span={spanId}&from={startMs}&to={end}&q={entity}&x={unknown}")), Ct);

        host.Opened.ShouldHaveSingleItem().ShouldBe(
            $"http://localhost:16686/trace/{TraceId}?span=00f067aa0ba902b7&from=1793694600000&to=2026-11-04T09%3A30%3A00.000Z&q=orders%20DLQ&x={{unknown}}");
        result.Text.ShouldBe($"Opened trace {TraceId} in localhost");
        host.Processes.ShouldBeEmpty();
    }

    [Test]
    public async Task Run_WithANonUrlTemplate_SaysSo()
    {
        using var host = new FakePluginHost();

        var error = await Should.ThrowAsync<InvalidOperationException>(new OpenTraceAction().RunAsync(Context(host, settings: ("url", "trace {traceId}")), Ct));

        error.Message.ShouldBe($"plugins.trace.url is not an absolute URL: trace {TraceId}");
        host.Opened.ShouldBeEmpty();
    }

    [Test]
    public async Task Run_FindsTheOnlyApplicationInsightsOfTheSubscription()
    {
        using var host = new FakePluginHost { Run = _ => new ProcessResult(0, AppInsightsId + "\n", "") };

        var result = await new OpenTraceAction().RunAsync(Context(host), Ct);

        host.Processes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            spec => spec.FileName.ShouldBe("az"),
            spec => spec.Arguments.ShouldBe(
                ["resource", "list", "--subscription", "sub-1", "--resource-type", "microsoft.insights/components", "--query", "[].id", "--output", "tsv"]));
        PortalSegments(host.Opened.ShouldHaveSingleItem())["resourceId"].ShouldBe(AppInsightsId);
        result.Text.ShouldBe($"Opened trace {TraceId} in Application Insights ai-orders");
    }

    [Test]
    public async Task Run_WithSeveralApplicationInsights_AsksToChoose_AndTakesOneByName()
    {
        const string other = "/subscriptions/sub-1/resourceGroups/rg-web/providers/microsoft.insights/components/ai-web";
        using var host = new FakePluginHost { Run = _ => new ProcessResult(0, $"{AppInsightsId}\n{other}\n", "") };

        var error = await Should.ThrowAsync<InvalidOperationException>(new OpenTraceAction().RunAsync(Context(host), Ct));
        await new OpenTraceAction().RunAsync(Context(host, settings: ("appInsights", "AI-WEB")), Ct);

        error.Message.ShouldBe("Choose an Application Insights (ai-orders, ai-web): azsb config set plugins.trace.appInsights <name>");
        PortalSegments(host.Opened.ShouldHaveSingleItem())["resourceId"].ShouldBe(other);
    }

    [Test]
    public async Task Run_WithoutApplicationInsights_SaysHowToSetOne()
    {
        using var host = new FakePluginHost { Run = _ => new ProcessResult(0, "", "") };

        var none = await Should.ThrowAsync<InvalidOperationException>(new OpenTraceAction().RunAsync(Context(host), Ct));
        var unknown = await Should.ThrowAsync<InvalidOperationException>(new OpenTraceAction().RunAsync(Context(host, settings: ("appInsights", "ai-x")), Ct));
        var emulator = await Should.ThrowAsync<InvalidOperationException>(new OpenTraceAction().RunAsync(Context(host, subscription: AzureSubscription.Emulator), Ct));

        none.Message.ShouldBe("No Application Insights in this subscription; set one: azsb config set plugins.trace.appInsights <resource id>");
        unknown.Message.ShouldBe("Application Insights ai-x not found in this subscription; set its resource id instead");
        emulator.Message.ShouldBe("Set where traces are: azsb config set plugins.trace.appInsights <resource id>, or plugins.trace.url <template>");
        host.Opened.ShouldBeEmpty();
    }

    [Test]
    public async Task FailingAzureCli_ReportsItsError()
    {
        using var host = new FakePluginHost { Run = _ => new ProcessResult(1, "", "ERROR: Please run 'az login' to setup account.") };

        var error = await Should.ThrowAsync<ProcessFailedException>(new OpenTraceAction().RunAsync(Context(host), Ct));

        error.Message.ShouldBe("az failed (exit code 1): ERROR: Please run 'az login' to setup account.");
    }

    [Test]
    public void Plugin_IsOnByDefault_WithATargetOrTheAzureCli()
    {
        using var host = new FakePluginHost();
        var plugin = new TracePlugin();
        var withAz = new PluginEnvironment(command => command == "az", _ => null);
        var withoutAz = new PluginEnvironment(_ => false, _ => null);

        plugin.Missing(Context(host).Settings, withAz).ShouldBeNull();
        plugin.Missing(Context(host).Settings, withoutAz).ShouldBe("az not found");
        plugin.Missing(Context(host, settings: ("appInsights", "ai-orders")).Settings, withoutAz).ShouldBe("az not found");
        plugin.Missing(Context(host, settings: ("appInsights", AppInsightsId)).Settings, withoutAz).ShouldBeNull();
        plugin.Missing(Context(host, settings: ("url", "http://localhost:16686/trace/{traceId}")).Settings, withoutAz).ShouldBeNull();
    }

    [Test]
    public void Plugin_DeclaresItsSettings_AndRegistersTheAction()
    {
        var plugin = new TracePlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);

        plugin.Id.ShouldBe("trace");
        plugin.Settings.Select(s => (s.Key, s.Default)).ShouldBe([("appInsights", null), ("tenant", null), ("url", null), ("cli", "az")]);
        services.ShouldHaveSingleItem().ImplementationType.ShouldBe(typeof(OpenTraceAction));
        new OpenTraceAction().ShouldSatisfyAllConditions(
            a => a.PluginId.ShouldBe(plugin.Id),
            a => a.Label.ShouldBe("Open trace"),
            a => a.Icon.ShouldBe("🔍"));
    }
}
