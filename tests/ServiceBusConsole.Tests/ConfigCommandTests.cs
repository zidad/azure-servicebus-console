using JasperFx.CommandLine;
using JasperFx.CommandLine.Help;
using Microsoft.Extensions.Hosting;

namespace ServiceBusConsole.Tests;

public sealed class ConfigCommandTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));
    private readonly StringWriter _out = new();
    private readonly StringWriter _error = new();

    private ConfigFile Config => new(Path.Combine(_directory, "config.json"));

    [After(Test)]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private ConfigInput Parse(params string[] args)
    {
        var run = new AppCommands(Host.CreateDefaultBuilder()).BuildRun(["config", .. args]);
        run.Command.ShouldBeOfType<ConfigCommand>();
        var input = run.Input.ShouldBeOfType<ConfigInput>();
        input.File = Config;
        input.Out = _out;
        input.Error = _error;
        return input;
    }

    private bool Run(params string[] args) => new ConfigCommand().Execute(Parse(args));

    private string[] Lines(StringWriter writer) => writer.ToString().Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);

    [Test]
    public void Config_IsInTheHelp_AndBuildsNoHost()
    {
        var commands = new AppCommands(Host.CreateDefaultBuilder());

        commands.AllCommandTypes().ShouldContain(typeof(ConfigCommand));
        commands.BuildRun(["help", "config"]).Input.ShouldBeOfType<HelpInput>().AppName.ShouldBe("azsb");
        commands.BuildRun(["config", "list"]).Input.ShouldNotBeAssignableTo<IHostBuilderInput>();
    }

    [Test]
    public void Parse_TakesTheActionKeyAndValue()
    {
        var input = Parse("set", "ui.theme", "light");

        input.Action.ShouldBe(ConfigAction.set);
        input.Key.ShouldBe("ui.theme");
        input.Value.ShouldBe("light");
        Parse("list", "--all").AllFlag.ShouldBeTrue();
    }

    [Test]
    public void Get_Unset_FailsWithoutOutput_LikeGit()
    {
        Run("get", "ui.theme").ShouldBeFalse();

        _out.ToString().ShouldBeEmpty();
        _error.ToString().ShouldBeEmpty();
    }

    [Test]
    public void Set_ThenGet_PrintsTheValue()
    {
        Run("set", "ui.theme", "light").ShouldBeTrue();
        Run("get", "UI.Theme").ShouldBeTrue();

        Lines(_out).ShouldBe(["light"]);
        _error.ToString().ShouldBeEmpty();
        Config.Get("ui.theme").ShouldBe("light");
    }

    [Test]
    [Arguments("ui.theme", false)]
    [Arguments("plugins.github.repo", false)]
    [Arguments("ui.colour", true)]
    public void Set_WarnsOnUnknownKeys_ButSetsThem(string key, bool warns)
    {
        Run("set", key, "x").ShouldBeTrue();

        Config.Get(key).ShouldBe("x");
        _error.ToString().Contains("unknown key").ShouldBe(warns);
    }

    [Test]
    public void Set_KnowsTheKeysItIsGiven()
    {
        var input = Parse("set", "ui.colour", "red");
        input.Keys = ConfigKeys.With([new ConfigKey("ui.colour", "A colour")]);

        new ConfigCommand().Execute(input).ShouldBeTrue();

        _error.ToString().ShouldBeEmpty();
    }

    [Test]
    public void Unset_RemovesTheValue_AndFailsWhenItIsNotSet()
    {
        Config.Set("ui.theme", "light");

        Run("unset", "ui.theme").ShouldBeTrue();
        Run("unset", "ui.theme").ShouldBeFalse();

        Config.Get("ui.theme").ShouldBeNull();
    }

    [Test]
    public void List_PrintsKeyValueLines()
    {
        Config.Set("ui.theme", "light");
        Config.Set("plugins.github.enabled", "true");

        Run("list").ShouldBeTrue();

        Lines(_out).ShouldBe(["plugins.github.enabled=true", "ui.theme=light"]);
    }

    [Test]
    public void ListAll_AddsUnsetKnownKeys_WithDefaultAndDescription()
    {
        Run("list", "--all").ShouldBeTrue();

        Lines(_out).ShouldBe([$"# ui.theme=auto  ({ConfigKeys.Theme.Description})"]);
    }

    [Test]
    public void ListAll_ShowsSetKnownKeysOnce()
    {
        Config.Set("ui.theme", "dark");

        Run("list", "--all").ShouldBeTrue();

        Lines(_out).ShouldBe(["ui.theme=dark"]);
    }

    [Test]
    public void Path_PrintsTheFilesPath()
    {
        Run("path").ShouldBeTrue();

        Lines(_out).ShouldBe([Config.FilePath]);
    }

    [Test]
    [Arguments("get")]
    [Arguments("set", "ui.theme")]
    [Arguments("list", "ui.theme")]
    [Arguments("path", "ui.theme")]
    public void MissingOrExtraArguments_PrintTheUsage(params string[] args)
    {
        Run(args).ShouldBeFalse();

        _error.ToString().ShouldStartWith("usage: azsb config");
    }

    [Test]
    public void InvalidKey_IsAnError()
    {
        Run("set", "theme", "light").ShouldBeFalse();

        _error.ToString().ShouldStartWith("error: Invalid key 'theme'");
    }

    [Test]
    public void BrokenFile_IsAnError()
    {
        Directory.CreateDirectory(_directory);
        File.WriteAllText(Config.FilePath, "{ not json");

        Run("get", "ui.theme").ShouldBeFalse();

        _error.ToString().ShouldStartWith("error:");
    }
}
