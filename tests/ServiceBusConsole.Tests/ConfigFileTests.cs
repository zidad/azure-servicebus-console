using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Tests;

public sealed class ConfigFileTests
{
    private readonly string _directory = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "azsb", "config.json");

    private ConfigFile Config => new(FilePath);

    private JsonObject Json => JsonNode.Parse(File.ReadAllText(FilePath))?.AsObject() ?? throw new InvalidDataException();

    [After(Test)]
    public void DeleteDirectory()
    {
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    [Test]
    public void MissingFile_IsEmpty_AndStaysMissing()
    {
        Config.Get("ui.theme").ShouldBeNull();
        Config.List().ShouldBeEmpty();
        Config.Unset("ui.theme").ShouldBeFalse();
        File.Exists(FilePath).ShouldBeFalse();
    }

    [Test]
    public void Set_CreatesTheDirectory_AndStoresNestedObjects()
    {
        Config.Set("ui.theme", "light");
        Config.Set("plugins.github.repo", "zidad/azure-servicebus-console");

        Config.Get("ui.theme").ShouldBe("light");
        File.ReadAllText(FilePath).ShouldBe(
            """
            {
              "ui": {
                "theme": "light"
              },
              "plugins": {
                "github": {
                  "repo": "zidad/azure-servicebus-console"
                }
              }
            }

            """);
        Directory.GetFiles(Path.GetDirectoryName(FilePath) ?? _directory).ShouldBe([FilePath]);
    }

    [Test]
    public void Keys_IgnoreCase_AndAreWrittenInLowerCase()
    {
        Config.Set("UI.Theme", "dark");
        Config.Set("ui.THEME", "light");

        Config.Get("Ui.Theme").ShouldBe("light");
        Config.List().ShouldBe([new ConfigEntry("ui.theme", "light")]);
    }

    [Test]
    public void HandWrittenKeys_AreFoundWhateverTheirCase_AndKept()
    {
        Directory.CreateDirectory(Path.GetDirectoryName(FilePath) ?? _directory);
        File.WriteAllText(FilePath, """
            {
              // comments and trailing commas, as IConfiguration allows
              "UI": { "Theme": "dark", },
              "other": { "kept": 1 }
            }
            """);

        Config.Get("ui.theme").ShouldBe("dark");
        Config.Set("ui.theme", "light");

        Config.List().ShouldBe([new ConfigEntry("other.kept", "1"), new ConfigEntry("UI.Theme", "light")]);
    }

    [Test]
    [Arguments("true", true)]
    [Arguments("False", false)]
    public void Set_StoresBooleans(string value, bool expected)
    {
        Config.Set("plugins.x.enabled", value);

        Json["plugins"]?["x"]?["enabled"]?.GetValue<bool>().ShouldBe(expected);
        Config.Get("plugins.x.enabled").ShouldBe(expected ? "true" : "false");
    }

    [Test]
    [Arguments("42", 42L)]
    [Arguments("-5", -5L)]
    public void Set_StoresIntegers(string value, long expected)
    {
        Config.Set("plugins.x.count", value);

        Json["plugins"]?["x"]?["count"]?.GetValue<long>().ShouldBe(expected);
        Config.Get("plugins.x.count").ShouldBe(value);
    }

    [Test]
    [Arguments("007")]
    [Arguments("1.5")]
    [Arguments("+3")]
    [Arguments("hello world")]
    [Arguments("ünïcode \"quoted\"")]
    public void Set_StoresAnythingElseAsText(string value)
    {
        Config.Set("plugins.x.text", value);

        Json["plugins"]?["x"]?["text"]?.GetValue<string>().ShouldBe(value);
        Config.Get("plugins.x.text").ShouldBe(value);
    }

    [Test]
    public void Set_ReplacesAValueWithASection_AndBack()
    {
        Config.Set("plugins.x", "flat");
        Config.Set("plugins.x.nested", "deep");
        Config.Get("plugins.x").ShouldBeNull();
        Config.Get("plugins.x.nested").ShouldBe("deep");

        Config.Set("plugins.x", "flat");
        Config.List().ShouldBe([new ConfigEntry("plugins.x", "flat")]);
    }

    [Test]
    public void Unset_RemovesTheValue_AndTheSectionsItEmpties()
    {
        Config.Set("ui.theme", "light");
        Config.Set("plugins.github.repo", "r");

        Config.Unset("plugins.github.repo").ShouldBeTrue();

        Json.ContainsKey("plugins").ShouldBeFalse();
        Config.List().ShouldBe([new ConfigEntry("ui.theme", "light")]);
    }

    [Test]
    public void Unset_KeepsSectionsThatStillHoldValues()
    {
        Config.Set("plugins.github.repo", "r");
        Config.Set("plugins.github.branch", "main");

        Config.Unset("PLUGINS.GitHub.Repo").ShouldBeTrue();

        Config.List().ShouldBe([new ConfigEntry("plugins.github.branch", "main")]);
    }

    [Test]
    public void Unset_LeavesMissingKeysAndWholeSections()
    {
        Config.Set("plugins.github.repo", "r");

        Config.Unset("plugins.github").ShouldBeFalse();
        Config.Unset("plugins.github.repo.deeper").ShouldBeFalse();
        Config.Unset("plugins.gitlab.repo").ShouldBeFalse();

        Config.Get("plugins.github.repo").ShouldBe("r");
    }

    [Test]
    public void List_FlattensAndSortsByKey()
    {
        Config.Set("ui.theme", "light");
        Config.Set("plugins.b.x", "2");
        Config.Set("plugins.a.x", "true");

        Config.List().ShouldBe([
            new ConfigEntry("plugins.a.x", "true"),
            new ConfigEntry("plugins.b.x", "2"),
            new ConfigEntry("ui.theme", "light"),
        ]);
    }

    [Test]
    [Arguments("theme")]
    [Arguments("ui.")]
    [Arguments(".theme")]
    [Arguments("ui..theme")]
    [Arguments("ui:theme")]
    [Arguments("ui theme")]
    public void InvalidKeys_Throw(string key)
    {
        Should.Throw<ArgumentException>(() => Config.Set(key, "x"));
        File.Exists(FilePath).ShouldBeFalse();
    }

    [Test]
    public void IConfiguration_ReadsTheKeysWithColons()
    {
        Config.Set("ui.theme", "light");
        Config.Set("plugins.github.enabled", "true");

        var configuration = new ConfigurationBuilder().AddJsonFile(FilePath).Build();

        configuration[ConfigKeys.Theme.ConfigurationPath].ShouldBe("light");
        configuration["plugins:github:enabled"].ShouldBe("True");
    }
}
