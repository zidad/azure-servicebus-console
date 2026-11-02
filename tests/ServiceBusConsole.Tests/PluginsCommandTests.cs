using JasperFx.CommandLine.Help;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.Tests;

public sealed class PluginsCommandTests
{
    private readonly StringWriter _out = new();

    private static PluginCatalog Catalog => new(
    [
        new FakePlugin(),
        new FakePlugin { Id = "other", Name = "Other", Description = "Another one", Settings = [] },
        new FakePlugin { Id = "tool", Name = "Tool", Description = "Runs a tool", Settings = [], Missing = (_, _) => "tool not found" },
    ]);

    private string[] Run(PluginCatalog catalog, params (string Key, string? Value)[] settings)
    {
        var run = new AppCommands(Host.CreateDefaultBuilder(), catalog).BuildRun(["plugins"]);
        var input = run.Input.ShouldBeOfType<PluginsInput>();
        input.Catalog.ShouldBeSameAs(catalog);
        input.Configuration = new ConfigurationBuilder().AddInMemoryCollection(settings.Select(s => KeyValuePair.Create(s.Key, s.Value))).Build();
        input.Out = _out;
        new PluginsCommand().Execute(input).ShouldBeTrue();
        return _out.ToString().Split(Environment.NewLine);
    }

    [Test]
    public void Plugins_IsInTheHelp_AndBuildsNoHost()
    {
        var commands = new AppCommands(Host.CreateDefaultBuilder());

        commands.AllCommandTypes().ShouldContain(typeof(PluginsCommand));
        commands.BuildRun(["help", "plugins"]).Input.ShouldBeOfType<HelpInput>();
        commands.BuildRun(["plugins"]).Command.ShouldBeOfType<PluginsCommand>();
    }

    [Test]
    public void Plugins_ListsEachWithItsStateAndSettings_SecretsMasked()
    {
        var lines = Run(Catalog, ("plugins:fake:token", "s3cret"), ("plugins:other:enabled", "false"));

        lines.ShouldBe(
        [
            "fake  Fake  enabled",
            "  Does fake things with messages",
            "  plugins.fake.enabled=  (default: auto; Turns the Fake plugin on or off; auto: on when what it needs is found)",
            "  plugins.fake.repo=  (default: zidad/example; Repository to file issues in)",
            "  plugins.fake.token=********  (default: none; API token)",
            "",
            "other  Other  disabled",
            "  Another one",
            "  plugins.other.enabled=false  (default: auto; Turns the Other plugin on or off; auto: on when what it needs is found)",
            "",
            "tool  Tool  disabled (tool not found)",
            "  Runs a tool",
            "  plugins.tool.enabled=  (default: auto; Turns the Tool plugin on or off; auto: on when what it needs is found)",
            "",
        ]);
    }

    [Test]
    public void NoPlugins_SaysSo() => Run(PluginCatalog.Empty).ShouldBe(["No plugins installed.", ""]);

    [Test]
    public void Config_KnowsThePluginsKeys_AndMasksSecretsInList()
    {
        var input = new AppCommands(Host.CreateDefaultBuilder(), Catalog).BuildRun(["config", "list"]).Input.ShouldBeOfType<ConfigInput>();
        var directory = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));
        var file = new ConfigFile(Path.Combine(directory, "config.json"));
        file.Set("plugins.fake.token", "s3cret");
        file.Set("plugins.fake.repo", "acme/orders");
        input.File = file;
        input.Out = _out;

        input.Keys.Select(k => k.Key).ShouldContain("plugins.fake.token");
        input.Keys.Select(k => k.Key).ShouldContain(ConfigKeys.Theme.Key);
        new ConfigCommand().Execute(input).ShouldBeTrue();
        _out.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries).ShouldBe(["plugins.fake.repo=acme/orders", "plugins.fake.token=********"]);
        Directory.Delete(directory, recursive: true);
    }
}
