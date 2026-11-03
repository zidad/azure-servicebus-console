namespace ServiceBusConsole.Tests;

public sealed class CommandSearchTests
{
    [Test]
    public void Find_LooksInThePathFolders_ForAnExecutable()
    {
        if (OperatingSystem.IsWindows()) return;
        var empty = Directory.CreateTempSubdirectory("azsb-path-");
        var bin = Directory.CreateTempSubdirectory("azsb-path-");
        var tool = Path.Combine(bin.FullName, "tool");
        var notExecutable = Path.Combine(bin.FullName, "readme");
        File.WriteAllText(tool, "#!/bin/sh\n");
        File.SetUnixFileMode(tool, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        File.WriteAllText(notExecutable, "");
        var path = string.Join(Path.PathSeparator, empty.FullName, bin.FullName);

        CommandSearch.Find("tool", path).ShouldBe(tool);
        CommandSearch.Find(tool, "").ShouldBe(tool);
        CommandSearch.Find("readme", path).ShouldBeNull();
        CommandSearch.Find("missing", path).ShouldBeNull();
        CommandSearch.Find("", path).ShouldBeNull();
        empty.Delete();
        bin.Delete(recursive: true);
    }

    [Test]
    public void SearchPath_AddsTheUsualInstallFolders_AfterPath()
    {
        if (OperatingSystem.IsWindows()) return;
        var sep = Path.PathSeparator;

        CommandSearch.SearchPath($"/usr/bin{sep}/usr/local/bin", "/Users/me")
            .ShouldBe($"/usr/bin{sep}/usr/local/bin{sep}/opt/homebrew/bin{sep}/Users/me/.local/bin");
        CommandSearch.SearchPath(null, "/Users/me").ShouldBe($"/opt/homebrew/bin{sep}/usr/local/bin{sep}/Users/me/.local/bin");
    }

    [Test]
    public void MissingCommand_NamesTheCommandNotFound()
    {
        var environment = new PluginEnvironment(command => command == "gh", _ => null);

        environment.MissingCommand("gh").ShouldBeNull();
        environment.MissingCommand("glab").ShouldBe("glab not found");
        environment.MissingCommand("").ShouldBe("no command set");
        environment.MissingCommand(null).ShouldBe("no command set");
    }
}
