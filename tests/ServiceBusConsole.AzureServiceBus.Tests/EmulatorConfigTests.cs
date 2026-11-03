namespace ServiceBusConsole.AzureServiceBus.Tests;

public sealed class EmulatorConfigTests
{
    [Test]
    public void ParsesTheRepositoryEmulatorConfig()
    {
        var config = EmulatorConfigReader.Parse(File.ReadAllText(RepositoryFile("emulator", "Config.json"))).ShouldNotBeNull();

        config.Queues.ShouldNotBeEmpty();
        config.Queues.ShouldAllBe(q => q.Name.Length > 0);
        config.Topics.SelectMany(t => t.Subscriptions).ShouldNotBeEmpty();
    }

    [Test]
    public void MissingListsAreEmpty()
    {
        var config = EmulatorConfigReader.Parse("""{ "UserConfig": { "Namespaces": [ { "Name": "sbemulatorns" } ] } }""").ShouldNotBeNull();

        config.Queues.ShouldBeEmpty();
        config.Topics.ShouldBeEmpty();
    }

    [Test]
    public void Find_LooksUpFromTheWorkingDirectory_ThenFromTheAppsFolder()
    {
        var root = Directory.CreateTempSubdirectory("azsb-emulator-");
        var repo = Directory.CreateDirectory(Path.Combine(root.FullName, "repo"));
        var config = Path.Combine(Directory.CreateDirectory(Path.Combine(repo.FullName, "emulator")).FullName, "Config.json");
        File.WriteAllText(config, "{}");
        var app = Directory.CreateDirectory(Path.Combine(repo.FullName, "src", "bin")).FullName;
        var elsewhere = Directory.CreateDirectory(Path.Combine(root.FullName, "other", "project")).FullName;

        EmulatorConfigReader.Find(Path.Combine(repo.FullName, "src"), elsewhere).ShouldBe(config);
        EmulatorConfigReader.Find(elsewhere, app).ShouldBe(config);
        EmulatorConfigReader.Find(elsewhere, elsewhere).ShouldBeNull();
        root.Delete(recursive: true);
    }

    private static string RepositoryFile(params string[] parts)
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
            if (Path.Combine([dir.FullName, .. parts]) is var path && File.Exists(path))
                return path;
        throw new FileNotFoundException(Path.Combine(parts));
    }
}
