using Microsoft.Extensions.Configuration;

namespace ServiceBusConsole.Tests;

// Sets AZSB_UI__THEME, which is process-wide.
[NotInParallel(nameof(ThemeSettingTests))]
public sealed class ThemeSettingTests
{
    private const string Variable = "AZSB_UI__THEME";

    private readonly string _directory = Path.Combine(Path.GetTempPath(), "azsb-tests", Guid.NewGuid().ToString("N"));

    private string FilePath => Path.Combine(_directory, "missing", "config.json");

    [Before(Test)]
    public void ClearVariable() => Environment.SetEnvironmentVariable(Variable, null);

    [After(Test)]
    public void Cleanup()
    {
        Environment.SetEnvironmentVariable(Variable, null);
        if (Directory.Exists(_directory)) Directory.Delete(_directory, recursive: true);
    }

    private string? Theme(string? flag) =>
        new RunInput { ThemeFlag = flag }.ThemeSetting(new ConfigurationBuilder().AddAzsbConfiguration(FilePath).Build());

    [Test]
    public void Nothing_IsAuto() => Theme(null).ShouldBeNull();

    [Test]
    public void File_SetsTheTheme()
    {
        new ConfigFile(FilePath).Set("ui.theme", "light");

        Theme(null).ShouldBe("light");
    }

    [Test]
    public void Environment_WinsOverTheFile()
    {
        new ConfigFile(FilePath).Set("ui.theme", "light");
        Environment.SetEnvironmentVariable(Variable, "dark");

        Theme(null).ShouldBe("dark");
    }

    [Test]
    public void Flag_WinsOverEverything()
    {
        new ConfigFile(FilePath).Set("ui.theme", "light");
        Environment.SetEnvironmentVariable(Variable, "dark");

        Theme("auto").ShouldBe("auto");
    }

    [Test]
    public void InvalidConfiguredTheme_HasNoPalette()
    {
        new ConfigFile(FilePath).Set("ui.theme", "solarized");

        Palette.For(Theme(null), null).ShouldBeNull();
    }
}
