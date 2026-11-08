using System.ComponentModel;
using System.Text;
using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;

namespace ServiceBusConsole.Plugins.Editor.Tests;

public sealed class OpenInEditorActionTests
{
    private static readonly MessageInfo Message = new()
    {
        SequenceNumber = 42,
        State = MessageState.Peeked,
        MessageId = "msg-42",
        Subject = "order-created",
        ContentType = "application/json",
        DeliveryCount = 3,
        DeadLetterReason = "MaxDeliveryCountExceeded",
        Properties = [new("tenant", "acme")],
        Body = Encoding.UTF8.GetBytes("""{"orderId": 7}"""),
    };

    private static readonly MessageSource Dlq = new MessageSource.Queue("orders", IsDlq: true);

    // Like the app: the plugin's declared defaults under the configured values.
    private static MessageActionContext Context(FakePluginHost host, MessageInfo? message = null, params (string Key, string Value)[] settings) => new()
    {
        PluginId = "editor",
        Message = message ?? Message,
        Source = Dlq,
        Session = new SessionSnapshot { At = DateTimeOffset.UnixEpoch },
        Settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new EditorPlugin().Settings.Where(s => s.Default is not null).Select(s => KeyValuePair.Create(s.Key, s.Default)))
            .AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, (string?)s.Value)))
            .Build(),
        Host = host,
        Progress = new Progress<string>(),
    };

    [Test]
    public void Plugin_IsOnByDefault_WhenItsEditorIsFound()
    {
        var plugin = new EditorPlugin();
        var environment = new PluginEnvironment(command => command == "code", _ => null);

        plugin.Missing(Context(new FakePluginHost()).Settings, environment).ShouldBeNull();
        plugin.Missing(Context(new FakePluginHost(), settings: ("command", "idea")).Settings, environment).ShouldBe("idea not found");
    }

    [Test]
    public async Task Run_WritesTheMessageAsJson_AndOpensItInCode()
    {
        using var host = new FakePluginHost();

        var result = await new OpenInEditorAction().RunAsync(Context(host), Ct);

        var file = Path.Combine(host.StateDirectory, "messages", "orders-dlq-42.json");
        result.ShouldBe(new ActionResult("Opened in code", FilePath: file));
        var json = JsonNode.Parse(await File.ReadAllTextAsync(file, Ct)).ShouldBeOfType<JsonObject>();
        json["sequenceNumber"]?.GetValue<long>().ShouldBe(42);
        json["subject"]?.GetValue<string>().ShouldBe("order-created");
        json["deadLetterReason"]?.GetValue<string>().ShouldBe("MaxDeliveryCountExceeded");
        json["properties"]?["tenant"]?.GetValue<string>().ShouldBe("acme");
        json["body"]?["orderId"]?.GetValue<int>().ShouldBe(7);
        host.Processes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            spec => spec.FileName.ShouldBe("code"),
            spec => spec.Arguments.ShouldBe([file]));
    }

    [Test]
    public async Task Run_WritesATextBodyAsAString_AndOverwritesTheSameFile()
    {
        using var host = new FakePluginHost();
        var action = new OpenInEditorAction();

        var first = await action.RunAsync(Context(host), Ct);
        var second = await action.RunAsync(Context(host, Message with { Body = Encoding.UTF8.GetBytes("not {json") }), Ct);

        second.FilePath.ShouldBe(first.FilePath);
        var file = second.FilePath.ShouldNotBeNull();
        JsonNode.Parse(await File.ReadAllTextAsync(file, Ct))?["body"]?.GetValue<string>().ShouldBe("not {json");
        Directory.GetFiles(Path.GetDirectoryName(file).ShouldNotBeNull()).ShouldHaveSingleItem();
    }

    [Test]
    public async Task Settings_OverrideTheCommandArgumentsAndDirectory()
    {
        using var host = new FakePluginHost();
        var directory = Path.Combine(host.StateDirectory, "my messages");

        var result = await new OpenInEditorAction().RunAsync(
            Context(host, null, ("command", "idea"), ("arguments", "--wait  --line=1:{file} {file}"), ("directory", directory)), Ct);

        var file = Path.Combine(directory, "orders-dlq-42.json");
        result.ShouldBe(new ActionResult("Opened in idea", FilePath: file));
        File.Exists(file).ShouldBeTrue();
        host.Processes.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            spec => spec.FileName.ShouldBe("idea"),
            spec => spec.Arguments.ShouldBe(["--wait", $"--line=1:{file}", file]));
    }

    [Test]
    public async Task Arguments_WithoutPlaceholder_GetTheFileAppended()
    {
        using var host = new FakePluginHost();

        await new OpenInEditorAction().RunAsync(Context(host, null, ("command", "subl"), ("arguments", "-n")), Ct);

        host.Processes.ShouldHaveSingleItem().Arguments.ShouldBe(["-n", Path.Combine(host.StateDirectory, "messages", "orders-dlq-42.json")]);
    }

    [Test]
    public async Task MissingExecutable_SaysHowToConfigureTheEditor()
    {
        using var host = new FakePluginHost
        {
            Run = spec => throw new InvalidOperationException($"Could not start {spec.FileName}", new Win32Exception(2, "No such file or directory")),
        };

        var error = await Should.ThrowAsync<InvalidOperationException>(new OpenInEditorAction().RunAsync(Context(host, null, ("command", "nano-x")), Ct));

        error.Message.ShouldBe("Could not start nano-x; set your editor: azsb config set plugins.editor.command <cmd>");
    }

    [Test]
    public async Task FailingEditor_ReportsItsError()
    {
        using var host = new FakePluginHost { Run = _ => new ProcessResult(1, "", "bad option") };

        var error = await Should.ThrowAsync<ProcessFailedException>(new OpenInEditorAction().RunAsync(Context(host), Ct));

        error.Message.ShouldBe("code failed (exit code 1): bad option");
    }

    [Test]
    public async Task EmptyCommand_SaysHowToSetIt()
    {
        using var host = new FakePluginHost();

        var error = await Should.ThrowAsync<InvalidOperationException>(new OpenInEditorAction().RunAsync(Context(host, null, ("command", "")), Ct));

        error.Message.ShouldBe("Set plugins.editor.command first: azsb config set plugins.editor.command <value>");
        host.Processes.ShouldBeEmpty();
    }

    [Test]
    public void Plugin_DeclaresItsSettings_AndRegistersTheAction()
    {
        var plugin = new EditorPlugin();
        var services = new ServiceCollection();

        plugin.ConfigureServices(services);

        plugin.Id.ShouldBe("editor");
        plugin.Settings.Select(s => (s.Key, s.Default)).ShouldBe([("command", "code"), ("arguments", null), ("directory", null)]);
        services.ShouldHaveSingleItem().ShouldSatisfyAllConditions(
            d => d.ServiceType.ShouldBe(typeof(IMessageAction)),
            d => d.ImplementationType.ShouldBe(typeof(OpenInEditorAction)));
        new OpenInEditorAction().ShouldSatisfyAllConditions(
            a => a.PluginId.ShouldBe(plugin.Id),
            a => a.Label.ShouldBe("Open in editor"),
            a => a.Icon.ShouldBe("📝"));
    }
}
